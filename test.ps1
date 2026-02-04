$ErrorActionPreference = "Stop"

$resultsDir = "$PSScriptRoot\TestResults"
if (Test-Path $resultsDir) { Remove-Item $resultsDir -Recurse -Force }

Write-Host "Running tests with coverage..." -ForegroundColor Cyan
dotnet test --collect:"XPlat Code Coverage" --results-directory $resultsDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "`nGenerating coverage report..." -ForegroundColor Cyan
reportgenerator -reports:"$resultsDir/**/coverage.cobertura.xml" -targetdir:"$resultsDir/CoverageReport" -reporttypes:"Html;TextSummary"

Get-Content "$resultsDir/CoverageReport/Summary.txt"

Write-Host "`nHTML report: $resultsDir\CoverageReport\index.htm" -ForegroundColor Green
