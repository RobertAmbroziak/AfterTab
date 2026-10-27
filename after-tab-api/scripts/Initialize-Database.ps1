param([switch]$Apply)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Tool restore failed.' }
    $migrations = @(Get-ChildItem 'src/AfterTab.DAL.EF/Migrations/*_InitialCreate.cs' -ErrorAction SilentlyContinue)
    if ($migrations.Count -eq 0) {
        dotnet ef migrations add InitialCreate --project src/AfterTab.DAL.EF --startup-project src/AfterTab.API --output-dir Migrations
        if ($LASTEXITCODE -ne 0) { throw 'Migration creation failed.' }
    }
    if ($Apply) {
        dotnet ef database update --project src/AfterTab.DAL.EF --startup-project src/AfterTab.API
        if ($LASTEXITCODE -ne 0) { throw 'Database update failed.' }
    } else {
        Write-Host 'Migration ready. To apply it: .\scripts\Initialize-Database.ps1 -Apply'
    }
} finally { Pop-Location }
