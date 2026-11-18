#Requires -Version 7.2
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:RepositoryRoot = Split-Path $PSScriptRoot
$script:SolutionDirectory = Join-Path $script:RepositoryRoot 'ncalc'

function Assert-Command {
    param([Parameter(Mandatory)][string]$Name)
    if (-not (Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue)) {
        throw "Required tool '$Name' was not found. See docs/development.md."
    }
}

function Invoke-CheckedNative {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$Arguments = @(),
        [string]$LogPath
    )
    # Explicitly check the exit code, including commands whose output is logged.
    if ($LogPath) {
        & $FilePath @Arguments 2>&1 | Tee-Object -FilePath $LogPath | Out-Host
    } else {
        & $FilePath @Arguments | Out-Host
    }
    if ($LASTEXITCODE -ne 0) {
        throw "'$FilePath' failed with exit code $LASTEXITCODE."
    }
}

function Assert-LockUpdateAllowed {
    param([switch]$UpdateLock)
    if ($UpdateLock -and $env:GITHUB_ACTIONS -eq 'true') {
        throw 'CI cannot update dependency locks. Regenerate and review them locally.'
    }
}

function Assert-DebugRuntime {
    param([Parameter(Mandatory)][string]$RuntimeConfigPath)
    $config = Get-Content -LiteralPath $RuntimeConfigPath -Raw | ConvertFrom-Json -AsHashtable
    $properties = $config.runtimeOptions.configProperties
    foreach ($feature in @('System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported', 'System.Runtime.InteropServices.BuiltInComInterop.IsSupported')) {
        if ($properties.ContainsKey($feature) -and $properties[$feature] -eq $false) {
            throw "Debug runtime disables $feature. Build Debug with PublishAot=false."
        }
    }
}

function Get-NativeMSBuild {
    if (-not $IsWindows) { throw 'WinUI builds require Windows. Use -CoreOnly for portable tests.' }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Install Visual Studio 2026; see docs/development.md.' }
    $installations = @(& $vswhere -latest -version '[18.0,19.0)' -products '*' -requires Microsoft.Component.MSBuild -property installationPath)
    if ($LASTEXITCODE -ne 0 -or $installations.Count -eq 0) { throw 'Visual Studio 2026 MSBuild was not found.' }
    $installation = $installations[0]
    $hostArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    $hostFolder = switch ($hostArchitecture) { 'Arm64' { 'arm64' } 'X64' { 'amd64' } default { '' } }
    $msbuild = Join-Path $installation "MSBuild/Current/Bin/$hostFolder/MSBuild.exe"
    if (-not (Test-Path -LiteralPath $msbuild)) { throw "Native $hostArchitecture MSBuild was not found: $msbuild" }
    $sdk = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/Platforms/UAP/10.0.26100.0/Platform.xml'
    if (-not (Test-Path -LiteralPath $sdk)) { throw 'Install Windows SDK 10.0.26100.0.' }
    return $msbuild
}
