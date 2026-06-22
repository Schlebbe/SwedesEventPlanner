param(
    [string] $StartupProject = "src/SwedesEventPlanner.Api/SwedesEventPlanner.Api.csproj",
    [string] $InfrastructureProject = "src/SwedesEventPlanner.Infrastructure/SwedesEventPlanner.Infrastructure.csproj"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../../..")
Set-Location $repoRoot

dotnet ef database update `
    --project $InfrastructureProject `
    --startup-project $StartupProject `
    --context EventPlannerDbContext
