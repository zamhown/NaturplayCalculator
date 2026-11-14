#Requires -Version 7.2
<#
.SYNOPSIS
Runs locked restore and regression tests, then optionally builds and verifies WinUI.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [ValidateSet('x64', 'x86', 'ARM64')][string]$Platform = 'x64',
    [switch]$CoreOnly,
    [switch]$SkipTests,
    [switch]$UpdateLock
)
. (Join-Path $PSScriptRoot 'common.ps1')
if ($CoreOnly -and $SkipTests) { throw 'CoreOnly and SkipTests cannot be combined.' }
Assert-LockUpdateAllowed -UpdateLock:$UpdateLock
Assert-Command dotnet
if (-not $CoreOnly) { $msbuild = Get-NativeMSBuild }
$runName = if ($CoreOnly) { "portable-$Configuration" } else { "$Platform-$Configuration" }
$logDirectory = Join-Path $RepositoryRoot 'artifacts/checks'
$resultsDirectory = Join-Path $RepositoryRoot "artifacts/tests/$runName"
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
Push-Location $RepositoryRoot
try {
    if (-not $SkipTests) {
        $testProject = Join-Path $SolutionDirectory 'NaturplayCalculator.Tests/NaturplayCalculator.Tests.csproj'
        $locked = if ($UpdateLock) { 'false' } else { 'true' }
        Invoke-CheckedNative dotnet @('restore', $testProject, '--nologo', "-p:RestoreLockedMode=$locked") -LogPath (Join-Path $logDirectory "restore-$runName.log")
        Invoke-CheckedNative dotnet @('test', $testProject, '--no-restore', '--nologo', '-c', $Configuration, '--results-directory', $resultsDirectory, '--logger', 'trx;LogFileName=regression.trx') -LogPath (Join-Path $logDirectory "test-$runName.log")
    }
    if (-not $CoreOnly) {
        $appProject = Join-Path $SolutionDirectory 'NaturplayCalculator/NaturplayCalculator.csproj'
        $locked = if ($UpdateLock) { 'false' } else { 'true' }
        $package = if ($Configuration -eq 'Release') { 'true' } else { 'false' }
        $buildLog = Join-Path $logDirectory "build-$runName.log"
        Write-Host "Building $Platform with $msbuild"
        Invoke-CheckedNative $msbuild @($appProject, '/restore', '/t:Build', "/p:Configuration=$Configuration", "/p:Platform=$Platform", "/p:RestoreLockedMode=$locked", '/p:AppxPackageSigningEnabled=false', "/p:GenerateAppxPackageOnBuild=$package", '/nologo', '/verbosity:minimal', '/nr:false', "/flp:LogFile=$buildLog;Verbosity=normal;Encoding=UTF-8")
        if ($Configuration -eq 'Debug') {
            $runtimeConfig = Join-Path $SolutionDirectory "NaturplayCalculator/bin/$Platform/Debug/net10.0-windows10.0.26100.0/win-$($Platform.ToLowerInvariant())/NaturplayCalculator.runtimeconfig.json"
            Assert-DebugRuntime -RuntimeConfigPath $runtimeConfig
        }
        if ($Configuration -eq 'Release') {
            [xml]$manifest = Get-Content -LiteralPath (Join-Path $SolutionDirectory 'NaturplayCalculator/Package.appxmanifest') -Raw
            $packageName = "NaturplayCalculator_$($manifest.Package.Identity.Version)_$($Platform.ToLowerInvariant())"
            $packagePath = Join-Path $SolutionDirectory "NaturplayCalculator/AppPackages/${packageName}_Test/$packageName.msix"
            & (Join-Path $PSScriptRoot 'check-package.ps1') -PackagePath $packagePath -Platform $Platform
        }
    }
}
finally { Pop-Location }
