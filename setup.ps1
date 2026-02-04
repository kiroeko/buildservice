$ErrorActionPreference = "Stop"

Write-Host "=== BuilderService Dev Setup ===" -ForegroundColor Cyan

# .NET global tools
Write-Host "`nInstalling .NET global tools..." -ForegroundColor Yellow
dotnet tool install -g dotnet-reportgenerator-globaltool
if ($LASTEXITCODE -ne 0) { dotnet tool update -g dotnet-reportgenerator-globaltool }

# Restore NuGet packages
Write-Host "`nRestoring NuGet packages..." -ForegroundColor Yellow
dotnet restore

Write-Host "`nSetup complete!" -ForegroundColor Green
