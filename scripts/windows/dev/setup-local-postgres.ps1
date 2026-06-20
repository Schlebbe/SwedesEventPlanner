[CmdletBinding()]
param(
    [string]$HostName = "localhost",
    [int]$Port = 5432,
    [string]$AdminDatabase = "postgres",
    [string]$AppRole = "swedesevents_dev",
    [string]$AppDatabase = "swedeseventplanner_dev",
    [securestring]$AppDatabasePassword,
    [string]$PsqlPath = "",
    [string]$HealthBaseUrl = "http://localhost:5088",
    [switch]$SkipHealthCheck,
    [switch]$SkipMigrations,
    [switch]$NoPause
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. "$PSScriptRoot\..\common.ps1"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$ApiProject = Join-Path $RepoRoot "src\SwedesEventPlanner.Api\SwedesEventPlanner.Api.csproj"
$WorkerProject = Join-Path $RepoRoot "src\SwedesEventPlanner.Worker\SwedesEventPlanner.Worker.csproj"
$InfrastructureProject = Join-Path $RepoRoot "src\SwedesEventPlanner.Infrastructure\SwedesEventPlanner.Infrastructure.csproj"

$script:SecretsToRedact = New-Object System.Collections.Generic.List[string]

function Add-SecretForRedaction {
    param([string]$Value)

    if (-not [string]::IsNullOrWhiteSpace($Value) -and -not $script:SecretsToRedact.Contains($Value)) {
        [void]$script:SecretsToRedact.Add($Value)
    }
}

function Redact-Text {
    param([AllowNull()][string]$Text)

    if ($null -eq $Text) {
        return ""
    }

    $redacted = $Text
    foreach ($secret in $script:SecretsToRedact) {
        if (-not [string]::IsNullOrEmpty($secret)) {
            $redacted = $redacted.Replace($secret, "<redacted>")
        }
    }

    return $redacted
}

function ConvertFrom-SecureStringToPlainText {
    param([Parameter(Mandatory = $true)][securestring]$SecureValue)

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        if ($bstr -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
    }
}

function New-RandomLocalPassword {
    $bytes = New-Object byte[] 32
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
        return [Convert]::ToBase64String($bytes).TrimEnd("=").Replace("+", "-").Replace("/", "_")
    }
    finally {
        $generator.Dispose()
    }
}

function Resolve-PsqlExecutable {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (Test-Path -LiteralPath $RequestedPath) {
            return (Resolve-Path -LiteralPath $RequestedPath).Path
        }

        throw "psql.exe was not found at '$RequestedPath'."
    }

    $command = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $candidate = Get-ChildItem "C:\Program Files\PostgreSQL" -Filter psql.exe -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if ($null -ne $candidate) {
        return $candidate.FullName
    }

    throw "Could not find psql.exe. Install PostgreSQL client tools or pass -PsqlPath."
}

function Quote-PostgresIdentifier {
    param([Parameter(Mandatory = $true)][string]$Value)
    return '"' + $Value.Replace('"', '""') + '"'
}

function Quote-PostgresLiteral {
    param([Parameter(Mandatory = $true)][string]$Value)
    return "'" + $Value.Replace("'", "''") + "'"
}

function Quote-ProcessArgument {
    param([Parameter(Mandatory = $true)][string]$Value)

    if ($Value -notmatch '[\s"]') {
        return $Value
    }

    return '"' + $Value.Replace('"', '\"') + '"'
}

function Invoke-PsqlSql {
    param(
        [Parameter(Mandatory = $true)][string]$Database,
        [Parameter(Mandatory = $true)][string]$Sql,
        [Parameter(Mandatory = $true)][string]$AdminUser,
        [Parameter(Mandatory = $true)][securestring]$AdminPassword,
        [Parameter(Mandatory = $true)][string]$ResolvedPsqlPath
    )

    $plainAdminPassword = ConvertFrom-SecureStringToPlainText $AdminPassword
    Add-SecretForRedaction $plainAdminPassword

    $previousPassword = $env:PGPASSWORD
    $hadPreviousPassword = Test-Path Env:\PGPASSWORD

    try {
        $env:PGPASSWORD = $plainAdminPassword
        $output = $Sql | & $ResolvedPsqlPath `
            -h $HostName `
            -p $Port `
            -U $AdminUser `
            -d $Database `
            -v "ON_ERROR_STOP=1" `
            -q `
            -f - 2>&1

        if ($LASTEXITCODE -ne 0) {
            throw "psql failed: $(Redact-Text ($output -join [Environment]::NewLine))"
        }

        return $output
    }
    finally {
        if ($hadPreviousPassword) {
            $env:PGPASSWORD = $previousPassword
        }
        else {
            Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
        }
    }
}

function Get-ProjectUserSecretsId {
    param([Parameter(Mandatory = $true)][string]$ProjectPath)

    [xml]$projectXml = Get-Content -LiteralPath $ProjectPath
    $secretIds = @($projectXml.Project.PropertyGroup.UserSecretsId | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    if ($secretIds.Count -eq 0) {
        throw "Project does not define UserSecretsId: $ProjectPath"
    }

    return [string]$secretIds[0]
}

function Get-UserSecretsPath {
    param([Parameter(Mandatory = $true)][string]$ProjectPath)

    $secretId = Get-ProjectUserSecretsId $ProjectPath
    return Join-Path $env:APPDATA "Microsoft\UserSecrets\$secretId\secrets.json"
}

function Get-UserSecretsObject {
    param([Parameter(Mandatory = $true)][string]$ProjectPath)

    $path = Get-UserSecretsPath $ProjectPath

    if (-not (Test-Path -LiteralPath $path)) {
        return [pscustomobject]@{}
    }

    $content = Get-Content -LiteralPath $path -Raw
    if ([string]::IsNullOrWhiteSpace($content)) {
        return [pscustomobject]@{}
    }

    return $content | ConvertFrom-Json
}

function Get-UserSecretValue {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$Key
    )

    $secrets = Get-UserSecretsObject $ProjectPath
    $property = $secrets.PSObject.Properties[$Key]

    if ($null -eq $property) {
        return $null
    }

    return [string]$property.Value
}

function Set-UserSecretValue {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$Key,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $path = Get-UserSecretsPath $ProjectPath
    $directory = Split-Path -Parent $path

    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $secrets = Get-UserSecretsObject $ProjectPath
    $property = $secrets.PSObject.Properties[$Key]
    if ($null -eq $property) {
        $secrets | Add-Member -NotePropertyName $Key -NotePropertyValue $Value
    }
    else {
        $property.Value = $Value
    }

    $secrets |
        ConvertTo-Json -Depth 10 |
        Set-Content -LiteralPath $path -Encoding UTF8
}

function Get-ConnectionStringValue {
    param(
        [string]$ConnectionString,
        [string]$Key
    )

    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        return $null
    }

    foreach ($part in $ConnectionString.Split(";")) {
        if ([string]::IsNullOrWhiteSpace($part) -or -not $part.Contains("=")) {
            continue
        }

        $equalsIndex = $part.IndexOf("=")
        if ($equalsIndex -lt 0) {
            continue
        }

        $currentKey = $part.Substring(0, $equalsIndex).Trim()
        if ($currentKey.Equals($Key, [StringComparison]::OrdinalIgnoreCase)) {
            return $part.Substring($equalsIndex + 1)
        }
    }

    return $null
}

function Get-ExistingAppPassword {
    $apiConnectionString = Get-UserSecretValue $ApiProject "ConnectionStrings:DefaultConnection"
    $workerConnectionString = Get-UserSecretValue $WorkerProject "ConnectionStrings:DefaultConnection"
    $candidates = New-Object System.Collections.Generic.List[string]

    foreach ($connectionString in @($apiConnectionString, $workerConnectionString)) {
        $database = Get-ConnectionStringValue $connectionString "Database"
        $username = Get-ConnectionStringValue $connectionString "Username"
        $password = Get-ConnectionStringValue $connectionString "Password"

        if ($database -eq $AppDatabase -and $username -eq $AppRole -and -not [string]::IsNullOrWhiteSpace($password)) {
            if (-not $candidates.Contains($password)) {
                [void]$candidates.Add($password)
            }
        }
    }

    if ($candidates.Count -gt 1) {
        Write-Host "Existing API and Worker app DB passwords differ; using the API value and updating both projects to match." -ForegroundColor Yellow
    }

    if ($candidates.Count -gt 0) {
        return $candidates[0]
    }

    return $null
}

function New-AppConnectionString {
    param([Parameter(Mandatory = $true)][string]$Password)

    return "Host=$HostName;Port=$Port;Database=$AppDatabase;Username=$AppRole;Password=$Password"
}

function Invoke-ExternalCommand {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [hashtable]$Environment = @{}
    )

    $previousValues = @{}
    foreach ($key in $Environment.Keys) {
        $previousValues[$key] = [Environment]::GetEnvironmentVariable($key, "Process")
        [Environment]::SetEnvironmentVariable($key, [string]$Environment[$key], "Process")
    }

    try {
        $output = & $FilePath @Arguments 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "$(Split-Path -Leaf $FilePath) failed: $(Redact-Text ($output -join [Environment]::NewLine))"
        }

        return $output
    }
    finally {
        foreach ($key in $Environment.Keys) {
            [Environment]::SetEnvironmentVariable($key, $previousValues[$key], "Process")
        }
    }
}

function Test-HealthEndpoint {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [int]$TimeoutSeconds = 2
    )

    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSeconds
        return ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300)
    }
    catch {
        return $false
    }
}

function Invoke-HealthVerification {
    param([Parameter(Mandatory = $true)][string]$ConnectionString)

    $healthUrl = "$HealthBaseUrl/health"
    $readyUrl = "$HealthBaseUrl/health/ready"

    if ((Test-HealthEndpoint $healthUrl) -and (Test-HealthEndpoint $readyUrl)) {
        Write-OpResult -Success $true -Step "Verified API health endpoints" -Details "$healthUrl and $readyUrl"
        return
    }

    $dotnetCommand = Get-Command dotnet -ErrorAction Stop
    $arguments = @(
        "run",
        "--project",
        $ApiProject,
        "--no-launch-profile",
        "--urls",
        $HealthBaseUrl
    )

    Write-Host "Starting API temporarily for health verification at $HealthBaseUrl..."
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo.FileName = $dotnetCommand.Source
    $process.StartInfo.UseShellExecute = $false
    $process.StartInfo.RedirectStandardOutput = $true
    $process.StartInfo.RedirectStandardError = $true
    $process.StartInfo.CreateNoWindow = $true
    $process.StartInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development"
    $process.StartInfo.EnvironmentVariables["DOTNET_ENVIRONMENT"] = "Development"
    $process.StartInfo.EnvironmentVariables["ConnectionStrings__DefaultConnection"] = $ConnectionString

    $process.StartInfo.Arguments = ($arguments | ForEach-Object { Quote-ProcessArgument $_ }) -join " "

    $started = $false
    try {
        [void]$process.Start()
        $started = $true

        $deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
        while ([DateTimeOffset]::UtcNow -lt $deadline) {
            if ($process.HasExited) {
                $stdout = $process.StandardOutput.ReadToEnd()
                $stderr = $process.StandardError.ReadToEnd()
                Write-OpResult -Success $false -Step "Health verification skipped" -Details "API exited before health checks passed. $(Redact-Text ($stdout + [Environment]::NewLine + $stderr))" -NextStep "Run the API manually and browse $healthUrl and $readyUrl."
                return
            }

            if ((Test-HealthEndpoint $healthUrl) -and (Test-HealthEndpoint $readyUrl)) {
                Write-OpResult -Success $true -Step "Verified API health endpoints" -Details "$healthUrl and $readyUrl"
                return
            }

            Start-Sleep -Seconds 1
        }

        Write-OpResult -Success $false -Step "Health verification skipped" -Details "API did not become ready within 45 seconds." -NextStep "Run the API manually and browse $healthUrl and $readyUrl."
    }
    catch {
        Write-OpResult -Success $false -Step "Health verification skipped" -Details (Redact-Text $_.Exception.Message) -NextStep "Run the API manually and browse $healthUrl and $readyUrl."
    }
    finally {
        if ($started -and -not $process.HasExited) {
            $process.Kill()
            $process.WaitForExit()
        }
    }
}

try {
    Write-Host "Windows local dev PostgreSQL setup only. This script does not touch Raspberry Pi production."

    if (-not (Test-Path -LiteralPath $ApiProject)) {
        throw "Missing API project at $ApiProject"
    }
    if (-not (Test-Path -LiteralPath $WorkerProject)) {
        throw "Missing Worker project at $WorkerProject"
    }

    $adminUserInput = Read-Host "PostgreSQL admin username [postgres]"
    if ([string]::IsNullOrWhiteSpace($adminUserInput)) {
        $adminUser = "postgres"
    }
    else {
        $adminUser = $adminUserInput.Trim()
    }

    $adminPassword = Read-Host "PostgreSQL admin password for '$adminUser'" -AsSecureString
    if ($adminPassword.Length -eq 0) {
        throw "Admin password cannot be empty."
    }

    $resolvedPsqlPath = Resolve-PsqlExecutable $PsqlPath

    if ($PSBoundParameters.ContainsKey("AppDatabasePassword")) {
        $appPassword = ConvertFrom-SecureStringToPlainText $AppDatabasePassword
        Write-Host "Using app database password provided securely to the script."
    }
    else {
        $appPassword = Get-ExistingAppPassword
        if ([string]::IsNullOrWhiteSpace($appPassword)) {
            $appPassword = New-RandomLocalPassword
            Write-Host "Generated a new random local app database password."
        }
        else {
            Write-Host "Reusing existing local app database password from user-secrets."
        }
    }

    Add-SecretForRedaction $appPassword

    $quotedRole = Quote-PostgresIdentifier $AppRole
    $roleLiteral = Quote-PostgresLiteral $AppRole
    $quotedDatabase = Quote-PostgresIdentifier $AppDatabase
    $databaseLiteral = Quote-PostgresLiteral $AppDatabase
    $passwordLiteral = Quote-PostgresLiteral $appPassword

    $roleSql = @"
DO `$`$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = $roleLiteral) THEN
        CREATE ROLE $quotedRole LOGIN PASSWORD $passwordLiteral;
    ELSE
        ALTER ROLE $quotedRole WITH LOGIN PASSWORD $passwordLiteral;
    END IF;
END
`$`$;
"@

    [void](Invoke-PsqlSql -Database $AdminDatabase -Sql $roleSql -AdminUser $adminUser -AdminPassword $adminPassword -ResolvedPsqlPath $resolvedPsqlPath)
    Write-OpResult -Success $true -Step "Created or updated local PostgreSQL role" -Details $AppRole

    $databaseSql = @"
SELECT format('CREATE DATABASE %I OWNER %I', $databaseLiteral, $roleLiteral)
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = $databaseLiteral)
\gexec
ALTER DATABASE $quotedDatabase OWNER TO $quotedRole;
GRANT ALL PRIVILEGES ON DATABASE $quotedDatabase TO $quotedRole;
"@

    [void](Invoke-PsqlSql -Database $AdminDatabase -Sql $databaseSql -AdminUser $adminUser -AdminPassword $adminPassword -ResolvedPsqlPath $resolvedPsqlPath)
    Write-OpResult -Success $true -Step "Created or updated local PostgreSQL database" -Details $AppDatabase

    $schemaSql = @"
ALTER SCHEMA public OWNER TO $quotedRole;
GRANT ALL ON SCHEMA public TO $quotedRole;
"@

    [void](Invoke-PsqlSql -Database $AppDatabase -Sql $schemaSql -AdminUser $adminUser -AdminPassword $adminPassword -ResolvedPsqlPath $resolvedPsqlPath)
    Write-OpResult -Success $true -Step "Configured local database schema ownership" -Details "public schema owned by $AppRole"

    $connectionString = New-AppConnectionString $appPassword
    Add-SecretForRedaction $connectionString

    Set-UserSecretValue -ProjectPath $ApiProject -Key "ConnectionStrings:DefaultConnection" -Value $connectionString
    Set-UserSecretValue -ProjectPath $WorkerProject -Key "ConnectionStrings:DefaultConnection" -Value $connectionString
    Write-OpResult -Success $true -Step "Stored local app connection string in .NET user-secrets" -Details "API and Worker projects updated; password redacted."

    if ($SkipMigrations) {
        Write-OpResult -Success $true -Step "Skipped EF migration apply" -Details "Requested by -SkipMigrations."
    }
    else {
        $dotnetCommand = Get-Command dotnet -ErrorAction Stop
        $migrationArgs = @(
            "ef",
            "database",
            "update",
            "--project",
            $InfrastructureProject,
            "--startup-project",
            $ApiProject
        )
        $migrationEnv = @{
            "ASPNETCORE_ENVIRONMENT" = "Development"
            "DOTNET_ENVIRONMENT" = "Development"
            "ConnectionStrings__DefaultConnection" = $connectionString
        }

        [void](Invoke-ExternalCommand -FilePath $dotnetCommand.Source -Arguments $migrationArgs -Environment $migrationEnv)
        Write-OpResult -Success $true -Step "Applied existing EF Core migrations" -Details $AppDatabase
    }

    if ($SkipHealthCheck) {
        Write-OpResult -Success $true -Step "Skipped API health verification" -Details "Requested by -SkipHealthCheck."
    }
    else {
        Invoke-HealthVerification -ConnectionString $connectionString
    }

    Write-Host "Done. No PostgreSQL admin password was stored. App database password is stored only in local .NET user-secrets."
}
catch {
    Write-OpResult -Success $false -Step "Local PostgreSQL setup failed" -Details (Redact-Text $_.Exception.Message)
    exit 1
}
finally {
    Pause-IfRequested -NoPause:$NoPause
}
