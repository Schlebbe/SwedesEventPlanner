[CmdletBinding()]
param(
    [string] $StartupProject = "src/SwedesEventPlanner.Api/SwedesEventPlanner.Api.csproj",
    [string] $InfrastructureProject = "src/SwedesEventPlanner.Infrastructure/SwedesEventPlanner.Infrastructure.csproj",
    [string] $PsqlPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../../..")
Set-Location $repoRoot

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

function Get-ProjectUserSecretsId {
    param([Parameter(Mandatory = $true)][string]$ProjectPath)

    [xml]$projectXml = Get-Content -LiteralPath $ProjectPath
    $secretIds = @($projectXml.Project.PropertyGroup.UserSecretsId | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

    if ($secretIds.Count -eq 0) {
        throw "Project does not define UserSecretsId: $ProjectPath"
    }

    return [string]$secretIds[0]
}

function Get-UserSecretValue {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$Key
    )

    $secretId = Get-ProjectUserSecretsId $ProjectPath
    $path = Join-Path $env:APPDATA "Microsoft\UserSecrets\$secretId\secrets.json"
    if (-not (Test-Path -LiteralPath $path)) {
        return $null
    }

    $content = Get-Content -LiteralPath $path -Raw
    if ([string]::IsNullOrWhiteSpace($content)) {
        return $null
    }

    $secrets = $content | ConvertFrom-Json
    $property = $secrets.PSObject.Properties[$Key]
    if ($null -eq $property) {
        return $null
    }

    return [string]$property.Value
}

function Get-ConnectionStringValue {
    param(
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][string]$Key
    )

    foreach ($part in $ConnectionString.Split(";")) {
        if ([string]::IsNullOrWhiteSpace($part) -or -not $part.Contains("=")) {
            continue
        }

        $equalsIndex = $part.IndexOf("=")
        $currentKey = $part.Substring(0, $equalsIndex).Trim()
        if ($currentKey.Equals($Key, [StringComparison]::OrdinalIgnoreCase)) {
            return $part.Substring($equalsIndex + 1)
        }
    }

    return $null
}

function Get-AppConnectionString {
    $fromEnvironment = [Environment]::GetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Process")
    if (-not [string]::IsNullOrWhiteSpace($fromEnvironment)) {
        return $fromEnvironment
    }

    return Get-UserSecretValue -ProjectPath $StartupProject -Key "ConnectionStrings:DefaultConnection"
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

function Invoke-PsqlReset {
    param(
        [Parameter(Mandatory = $true)][string]$ResolvedPsqlPath,
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][string]$Port,
        [Parameter(Mandatory = $true)][string]$Database,
        [Parameter(Mandatory = $true)][string]$Username,
        [Parameter(Mandatory = $true)][string]$Password
    )

    $sql = @"
SET lock_timeout = '5s';
DROP SCHEMA IF EXISTS public CASCADE;
CREATE SCHEMA public;
"@

    $previousPassword = $env:PGPASSWORD
    $hadPreviousPassword = Test-Path Env:\PGPASSWORD

    try {
        $env:PGPASSWORD = $Password
        $output = $sql | & $ResolvedPsqlPath `
            -h $HostName `
            -p $Port `
            -U $Username `
            -d $Database `
            -v "ON_ERROR_STOP=1" `
            -q `
            -f - 2>&1

        if ($LASTEXITCODE -ne 0) {
            throw "psql failed: $(Redact-Text ($output -join [Environment]::NewLine))"
        }
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

try {
    Write-Host "Windows local dev database reset only. This script does not touch Raspberry Pi production."

    if (-not (Test-Path -LiteralPath $StartupProject)) {
        throw "Missing startup project at $StartupProject"
    }
    if (-not (Test-Path -LiteralPath $InfrastructureProject)) {
        throw "Missing infrastructure project at $InfrastructureProject"
    }

    $connectionString = Get-AppConnectionString
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        throw "No local app connection string was found. Run scripts/windows/dev/setup-local-postgres.ps1 first."
    }

    Add-SecretForRedaction $connectionString

    $hostName = Get-ConnectionStringValue $connectionString "Host"
    $port = Get-ConnectionStringValue $connectionString "Port"
    $database = Get-ConnectionStringValue $connectionString "Database"
    $username = Get-ConnectionStringValue $connectionString "Username"
    $password = Get-ConnectionStringValue $connectionString "Password"

    if ([string]::IsNullOrWhiteSpace($hostName)) {
        $hostName = "localhost"
    }
    if ([string]::IsNullOrWhiteSpace($port)) {
        $port = "5432"
    }
    if ([string]::IsNullOrWhiteSpace($database) -or [string]::IsNullOrWhiteSpace($username) -or [string]::IsNullOrWhiteSpace($password)) {
        throw "The configured local app connection string must include Database, Username, and Password. Run scripts/windows/dev/setup-local-postgres.ps1 to repair it."
    }

    Add-SecretForRedaction $password

    $resolvedPsqlPath = Resolve-PsqlExecutable $PsqlPath

    Write-Host "Resetting schema 'public' in local database '$database' as '$username'."
    Invoke-PsqlReset `
        -ResolvedPsqlPath $resolvedPsqlPath `
        -HostName $hostName `
        -Port $port `
        -Database $database `
        -Username $username `
        -Password $password
    Write-Host "OK: Cleared local app schema/data without dropping the database."

    $dotnetCommand = Get-Command dotnet -ErrorAction Stop
    $migrationArgs = @(
        "ef",
        "database",
        "update",
        "--project",
        $InfrastructureProject,
        "--startup-project",
        $StartupProject,
        "--context",
        "EventPlannerDbContext"
    )
    $migrationEnv = @{
        "ASPNETCORE_ENVIRONMENT" = "Development"
        "DOTNET_ENVIRONMENT" = "Development"
        "ConnectionStrings__DefaultConnection" = $connectionString
    }

    [void](Invoke-ExternalCommand -FilePath $dotnetCommand.Source -Arguments $migrationArgs -Environment $migrationEnv)
    Write-Host "OK: Applied EF Core migrations."
    Write-Host "Done. Local app data has been reset; the database itself was not dropped."
}
catch {
    $message = Redact-Text $_.Exception.Message
    Write-Host "FAIL: Local database reset failed." -ForegroundColor Red
    Write-Host "Details: $message"

    if ($message -match "does not exist" -or $message -match "No local app connection string" -or $message -match "connection string") {
        Write-Host "Next: Run scripts/windows/dev/setup-local-postgres.ps1 to create or repair the local role, database, user-secrets, and migrations."
    }
    elseif ($message -match "being accessed by other users" -or $message -match "lock" -or $message -match "permission denied" -or $message -match "dependent objects") {
        Write-Host "Next: Stop the API, Worker, pgAdmin query tabs, and any other local PostgreSQL clients, then run reset-local-database.ps1 again."
    }

    exit 1
}
