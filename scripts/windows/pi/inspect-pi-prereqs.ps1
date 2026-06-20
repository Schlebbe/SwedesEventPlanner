[CmdletBinding()]
param(
    [string]$HostOrIp = $env:PI_HOST_OR_IP,
    [string]$User = $(if ($env:PI_USER) { $env:PI_USER } else { "sebastian" }),
    [string]$KeyPath = $(if ($env:PI_SSH_KEY_PATH) { $env:PI_SSH_KEY_PATH } else { $eventKey = Join-Path $HOME ".codex\keys\swedeseventplanner-pi\.codex_pi_ed25519"; $trackerKey = Join-Path $HOME ".codex\keys\swedesclantracker-pi\.codex_pi_ed25519"; if (Test-Path -LiteralPath $eventKey) { $eventKey } elseif (Test-Path -LiteralPath $trackerKey) { $trackerKey } else { Join-Path $HOME ".ssh\id_ed25519" } }),
    [string]$KnownHostsPath = $(if ($env:PI_SSH_KNOWN_HOSTS_PATH) { $env:PI_SSH_KNOWN_HOSTS_PATH } else { $eventKnownHosts = Join-Path $HOME ".codex\keys\swedeseventplanner-pi\.codex_known_hosts"; $trackerKnownHosts = Join-Path $HOME ".codex\keys\swedesclantracker-pi\.codex_known_hosts"; if (Test-Path -LiteralPath $eventKnownHosts) { $eventKnownHosts } elseif (Test-Path -LiteralPath $trackerKnownHosts) { $trackerKnownHosts } else { Join-Path $HOME ".ssh\known_hosts" } }),
    [switch]$NoPause
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "pi-common.ps1")

try {
    $HostOrIp = Resolve-PiHost -HostOrIp $HostOrIp
    $User = Resolve-PiUser -User $User
    $KeyPath = Resolve-PathWithPrompt -PathValue $KeyPath -PromptLabel "SSH private key path"
    $KnownHostsPath = Resolve-PathWithPrompt -PathValue $KnownHostsPath -PromptLabel "SSH known_hosts path"

    $remoteScript = @'
set -eu
printf '== os ==\n'
uname -a
if [ -r /etc/os-release ]; then . /etc/os-release; printf '%s %s\n' "$PRETTY_NAME" "${VERSION_CODENAME:-}"; fi

printf '\n== dotnet runtimes ==\n'
command -v dotnet || true
dotnet --list-runtimes 2>/dev/null || true

printf '\n== required commands ==\n'
for cmd in dotnet psql rsync jq ssh scp; do
  if command -v "$cmd" >/dev/null 2>&1; then
    printf 'ok %s %s\n' "$cmd" "$(command -v "$cmd")"
  else
    printf 'missing %s\n' "$cmd"
  fi
done
if command -v nginx >/dev/null 2>&1; then
  printf 'ok nginx %s\n' "$(command -v nginx)"
elif [ -x /usr/sbin/nginx ]; then
  printf 'ok nginx /usr/sbin/nginx\n'
else
  printf 'missing nginx\n'
fi

printf '\n== versions ==\n'
(/usr/sbin/nginx -v || nginx -v) 2>&1 || true
psql --version 2>/dev/null || true
rsync --version 2>/dev/null | head -n 1 || true
jq --version 2>/dev/null || true

printf '\n== core service states ==\n'
systemctl is-active nginx postgresql 2>/dev/null || true
systemctl is-enabled nginx postgresql 2>/dev/null || true

printf '\n== sudo ==\n'
if sudo -n true 2>/dev/null; then echo 'sudo_nopasswd=ok'; else echo 'sudo_nopasswd=missing'; fi

printf '\n== listeners ==\n'
sudo -n ss -tlnp 2>/dev/null | sed -n '1,16p' || ss -tln 2>/dev/null | sed -n '1,16p' || true

printf '\n== existing swedes apps ==\n'
sudo -n find /opt -maxdepth 2 -type d \( -name 'swedes*' -o -name 'Swedes*' \) -printf '%M %u:%g %p\n' 2>/dev/null | sort || true

printf '\n== event planner paths ==\n'
for path in /opt/swedeseventplanner /etc/swedeseventplanner; do
  if sudo -n test -e "$path" 2>/dev/null; then
    sudo -n stat -c '%A %U:%G %n' "$path"
  else
    printf 'missing %s\n' "$path"
  fi
done
'@

    $scriptBytes = [Text.Encoding]::UTF8.GetBytes($remoteScript)
    $scriptB64 = [Convert]::ToBase64String($scriptBytes)
    $result = Invoke-Ssh -HostOrIp $HostOrIp -User $User -KeyPath $KeyPath -KnownHostsPath $KnownHostsPath -RemoteCommand "printf '$scriptB64' | base64 -d | bash"

    if ($result.Output) {
        $result.Output | Out-Host
    }

    if ($result.ExitCode -ne 0) {
        Write-OpResult -Success $false -Step "Pi prerequisite inspection failed" -Details "Exit code: $($result.ExitCode)" -NextStep "Run test-pi-ssh.ps1, then retry."
        Pause-IfRequested -NoPause:$NoPause
        exit 1
    }

    Write-OpResult -Success $true -Step "Pi prerequisite inspection completed" -Details "Printed read-only runtime, service, listener, and path inventory." -NextStep "Use this output before creating deployment scripts."
    Pause-IfRequested -NoPause:$NoPause
}
catch {
    Write-OpResult -Success $false -Step "Pi prerequisite inspection error" -Details $_.Exception.Message -NextStep "Confirm SSH credentials and Pi availability."
    Pause-IfRequested -NoPause:$NoPause
    exit 1
}
