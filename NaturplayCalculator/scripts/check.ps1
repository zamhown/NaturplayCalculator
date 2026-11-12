param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [ValidateSet('x64', 'x86', 'ARM64')][string]$Platform = 'x64',
    [switch]$CoreOnly,
    [switch]$SkipTests,
    [switch]$UpdateLock
)
$ErrorActionPreference = 'Stop'
$solutionDirectory = Split-Path $PSScriptRoot
$testProject = Join-Path $solutionDirectory 'NaturplayCalculator.Tests\NaturplayCalculator.Tests.csproj'
$appProject = Join-Path $solutionDirectory 'NaturplayCalculator\NaturplayCalculator.csproj'
$logDirectory = Join-Path (Split-Path $solutionDirectory) 'artifacts/checks'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
if ($CoreOnly -and $SkipTests) { throw 'CoreOnly and SkipTests cannot be combined.' }
Push-Location $solutionDirectory
try {
    if (-not $SkipTests) {
        $restoreArguments = @('restore', $testProject, '--nologo')
        if (-not $UpdateLock) { $restoreArguments += '--locked-mode' }
        & dotnet @restoreArguments
        if ($LASTEXITCODE -ne 0) { throw 'Test dependency restore failed.' }
        & dotnet test $testProject --no-restore --nologo -c $Configuration --logger "trx;LogFileName=regression-$Configuration.trx"
        if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
    }
    if (-not $CoreOnly) {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Install Visual Studio 2026 with the Windows application development tooling and Windows SDK 10.0.26100.0.' }
        $msbuild = & $vswhere -latest -version '[18.0,19.0)' -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if (-not $msbuild) { throw 'Visual Studio 2026 MSBuild was not found.' }
        $sdk = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\Platforms\UAP\10.0.26100.0\Platform.xml'
        if (-not (Test-Path -LiteralPath $sdk)) { throw 'Install Windows SDK 10.0.26100.0.' }
        $locked = if ($UpdateLock) { 'false' } else { 'true' }
        $package = if ($Configuration -eq 'Release') { 'true' } else { 'false' }
        $buildLog = Join-Path $logDirectory "build-$Platform-$Configuration.log"
        & $msbuild $appProject /restore /t:Build "/p:Configuration=$Configuration" "/p:Platform=$Platform" "/p:RestoreLockedMode=$locked" /p:AppxPackageSigningEnabled=false "/p:GenerateAppxPackageOnBuild=$package" /p:TreatWarningsAsErrors=true /nologo /verbosity:minimal /nr:false "/flp:LogFile=$buildLog;Verbosity=normal;Encoding=UTF-8"
        if ($LASTEXITCODE -ne 0) { throw 'WinUI 3 build failed.' }
        if ($Configuration -eq 'Release') {
            [xml]$manifest = Get-Content (Join-Path $solutionDirectory 'NaturplayCalculator/Package.appxmanifest')
            $packageName = "NaturplayCalculator_$($manifest.Package.Identity.Version)_$($Platform.ToLowerInvariant())"
            $packagePath = Join-Path $solutionDirectory "NaturplayCalculator/AppPackages/${packageName}_Test/$packageName.msix"
            & (Join-Path $PSScriptRoot 'check-package.ps1') -PackagePath $packagePath -Platform $Platform
        }
    }
}
finally { Pop-Location }
