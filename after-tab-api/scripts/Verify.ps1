$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    dotnet restore AfterTabApi.sln
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    dotnet build AfterTabApi.sln --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet test AfterTabApi.sln --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
} finally { Pop-Location }
