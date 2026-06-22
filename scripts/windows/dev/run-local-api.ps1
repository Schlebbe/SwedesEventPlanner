param(
    [string] $Project = "src/SwedesEventPlanner.Api/SwedesEventPlanner.Api.csproj"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../../..")
Set-Location $repoRoot

$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project $Project
