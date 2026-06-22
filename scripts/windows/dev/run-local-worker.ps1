param(
    [string] $Project = "src/SwedesEventPlanner.Worker/SwedesEventPlanner.Worker.csproj"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../../..")
Set-Location $repoRoot

$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --project $Project
