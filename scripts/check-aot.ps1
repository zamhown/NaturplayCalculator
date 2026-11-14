#Requires -Version 7.2
<#
.SYNOPSIS
Publishes the portable Native AOT smoke executable and runs it on a compatible host.
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-x86', 'win-arm64', 'linux-x64', 'osx-arm64')][string]$RuntimeIdentifier,
    [switch]$PublishOnly,
    [switch]$UpdateLock
)
. (Join-Path $PSScriptRoot 'common.ps1')
Assert-Command dotnet
Assert-LockUpdateAllowed -UpdateLock:$UpdateLock
$project = Join-Path $SolutionDirectory 'NaturplayCalculator.AotSmoke/NaturplayCalculator.AotSmoke.csproj'
$locked = if ($UpdateLock) { 'false' } else { 'true' }
$logDirectory = Join-Path $RepositoryRoot 'artifacts/checks'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
Push-Location $RepositoryRoot
try {
    if (-not $RuntimeIdentifier) {
        $output = & dotnet msbuild $project -nologo -getProperty:RuntimeIdentifier
        if ($LASTEXITCODE -ne 0) { throw 'Could not resolve the host runtime identifier.' }
        $RuntimeIdentifier = ($output -join '').Trim()
    }
    if ($RuntimeIdentifier -notin @('win-x64', 'win-x86', 'win-arm64', 'linux-x64', 'osx-arm64')) { throw "Unsupported runtime: $RuntimeIdentifier" }
    $hostRid = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
    $canExecute = $RuntimeIdentifier -eq $hostRid -or ($hostRid -eq 'win-x64' -and $RuntimeIdentifier -eq 'win-x86')
    if (-not $PublishOnly -and -not $canExecute) { throw "Cannot run $RuntimeIdentifier on $hostRid. Use -PublishOnly for cross-compilation." }
    # Do not use -r: it changes the portable libraries' restore graph and lock files.
    Invoke-CheckedNative dotnet @('publish', $project, '-c', 'Release', "-p:SmokeRuntimeIdentifier=$RuntimeIdentifier", '--nologo', "-p:RestoreLockedMode=$locked") -LogPath (Join-Path $logDirectory "aot-$RuntimeIdentifier.log")
    if ($PublishOnly) { Write-Host "Published $RuntimeIdentifier; execution was explicitly skipped."; return }
    $fileName = 'NaturplayCalculator.AotSmoke' + $(if ($IsWindows) { '.exe' } else { '' })
    Invoke-CheckedNative (Join-Path $SolutionDirectory "NaturplayCalculator.AotSmoke/bin/Release/net10.0/$RuntimeIdentifier/publish/$fileName") -LogPath (Join-Path $logDirectory "smoke-$RuntimeIdentifier.log")
}
finally { Pop-Location }