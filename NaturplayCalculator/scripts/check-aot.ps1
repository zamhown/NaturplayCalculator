param(
    [ValidateSet('win-x64', 'win-x86', 'win-arm64', 'linux-x64', 'osx-arm64')][string]$RuntimeIdentifier,
    [switch]$PublishOnly,
    [switch]$UpdateLock
)
$ErrorActionPreference = 'Stop'
$solutionDirectory = Split-Path $PSScriptRoot
$project = Join-Path $solutionDirectory 'NaturplayCalculator.AotSmoke/NaturplayCalculator.AotSmoke.csproj'
$locked = if ($UpdateLock) { 'false' } else { 'true' }
Push-Location $solutionDirectory
try {
    if (-not $RuntimeIdentifier) {
        $RuntimeIdentifier = (& dotnet msbuild $project -nologo -getProperty:RuntimeIdentifier).Trim()
        if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the host runtime identifier.' }
    }
    # Keep the RID local to the executable project. Passing -r would also change
    # the portable libraries' restore graph and invalidate their shared lock files.
    & dotnet publish $project -c Release "-p:SmokeRuntimeIdentifier=$RuntimeIdentifier" --nologo "-p:RestoreLockedMode=$locked"
    if ($LASTEXITCODE -ne 0) { throw 'Native AOT publish failed.' }
    if ($PublishOnly) { Write-Host "Published $RuntimeIdentifier; execution was explicitly skipped."; return }
    $fileName = 'NaturplayCalculator.AotSmoke' + $(if ($RuntimeIdentifier.StartsWith('win-')) { '.exe' } else { '' })
    & (Join-Path $solutionDirectory "NaturplayCalculator.AotSmoke/bin/Release/net10.0/$RuntimeIdentifier/publish/$fileName")
    if ($LASTEXITCODE -ne 0) { throw 'Native AOT smoke failed.' }
}
finally { Pop-Location }
