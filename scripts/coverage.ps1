# Runs the unit tests with code coverage and builds an HTML report.
# Scope is set in tests/coverage.runsettings (depot logic in Domain and Application).
# Usage (Aspire stopped):  powershell -ExecutionPolicy Bypass -File scripts/coverage.ps1
# Report:                  TestResults/CoverageReport/index.html
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$results = Join-Path $root 'TestResults'

Push-Location $root
try {
    # Start clean so results from older runs are not merged into the report
    if (Test-Path $results) { Remove-Item $results -Recurse -Force }

    dotnet test tests/Application.UnitTests `
        --collect:"XPlat Code Coverage" `
        --settings tests/coverage.runsettings `
        --results-directory $results
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    # ReportGenerator is a local tool (.config/dotnet-tools.json)
    dotnet tool restore
    dotnet reportgenerator `
        "-reports:$results/**/coverage.cobertura.xml" `
        "-targetdir:$results/CoverageReport" `
        "-reporttypes:Html;TextSummary"

    # Overall numbers first; the HTML report shows coverage per class and line
    Get-Content (Join-Path $results 'CoverageReport/Summary.txt') -TotalCount 15
}
finally {
    Pop-Location
}
