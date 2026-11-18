#requires -Version 7.2
[CmdletBinding()]
param(
    [switch]$MeasureOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$project = Join-Path $repoRoot 'math/tests/NaturplayMath.Tests/NaturplayMath.Tests.csproj'
$settings = Join-Path $repoRoot 'math/tests/coverage.runsettings'
$runName = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff')
$results = Join-Path $repoRoot "artifacts/math-coverage/$runName"

& dotnet test $project -c Release --settings $settings '--collect:XPlat Code Coverage' --logger trx --results-directory $results --blame-hang-timeout 2m
if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE." }

$testReports = @(Get-ChildItem -LiteralPath $results -Filter '*.trx' -Recurse -File)
if ($testReports.Count -ne 1) { throw "Expected one test report; found $($testReports.Count)." }
[xml]$testReport = Get-Content -LiteralPath $testReports[0].FullName -Raw
$counters = $testReport.TestRun.ResultSummary.Counters
if ([long]$counters.total -le 0 -or [long]$counters.passed -ne [long]$counters.total) {
    throw 'All tests must execute and pass; skipped or empty test runs do not satisfy the gate.'
}

$reports = @(Get-ChildItem -LiteralPath $results -Filter coverage.cobertura.xml -Recurse -File)
# VSTest also copies collector attachments into the TRX run's In/ directory.
# Accept identical copies, but never silently choose among different reports.
$reportHashes = @($reports | Get-FileHash -Algorithm SHA256 | Select-Object -ExpandProperty Hash -Unique)
if ($reportHashes.Count -ne 1) { throw "Expected one distinct coverage report; found $($reportHashes.Count)." }
[xml]$report = Get-Content -LiteralPath $reports[0].FullName -Raw
$expected = @('NaturplayMath', 'NaturplayMath.Core', 'NaturplayMath.Numerics', 'NaturplayMath.Symbolics')
$packages = @($report.coverage.packages.package)
$names = @($packages | ForEach-Object { $_.name } | Sort-Object)
if (@(Compare-Object $expected $names).Count -ne 0) { throw 'Coverage must include all four production assemblies.' }

$summary = [ordered]@{
    testsPassed = [long]$counters.passed
    testsTotal = [long]$counters.total
    linesCovered = [long]$report.coverage.'lines-covered'
    linesTotal = [long]$report.coverage.'lines-valid'
    branchesCovered = [long]$report.coverage.'branches-covered'
    branchesTotal = [long]$report.coverage.'branches-valid'
    assemblies = $names
    report = $reports[0].FullName
}
if ($summary.linesTotal -le 0 -or $summary.branchesTotal -le 0) { throw 'The coverage report is empty.' }
$summary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $results 'summary.json') -Encoding utf8
Write-Host "Lines: $($summary.linesCovered)/$($summary.linesTotal); branches: $($summary.branchesCovered)/$($summary.branchesTotal)"
Write-Host "Reports: $results"
if (-not $MeasureOnly -and ($summary.linesCovered -ne $summary.linesTotal -or $summary.branchesCovered -ne $summary.branchesTotal)) {
    throw 'NaturplayMath requires exactly 100% line and branch coverage; no percentage rounding is used.'
}
