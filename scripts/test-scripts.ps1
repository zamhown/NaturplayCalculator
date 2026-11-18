#Requires -Version 7.2
<#
.SYNOPSIS
Exercises build-script failure handling and MSIX/preview boundaries without installing an app.
#>
[CmdletBinding()]
param()
. (Join-Path $PSScriptRoot 'common.ps1')
Assert-Command pwsh
$testParent = Join-Path $RepositoryRoot 'artifacts/script-tests'
$testRoot = Join-Path $testParent ([guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$script:Passed = 0

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Test-Case {
    param([string]$Name, [scriptblock]$Action)
    & $Action
    $script:Passed++
    Write-Host "PASS: $Name"
}

function Assert-Failure {
    param([scriptblock]$Action, [string]$MessagePattern)
    $caught = $null
    try { & $Action } catch { $caught = $_ }
    if (-not $caught) { throw "Expected failure matching '$MessagePattern'." }
    if ($caught.Exception.Message -notlike $MessagePattern) { throw "Unexpected failure: $($caught.Exception.Message)" }
}

function New-PackageFixture {
    param(
        [string]$Name,
        [switch]$Managed,
        [switch]$Truncated,
        [switch]$MissingRuntime,
        [switch]$UnsafeEntry,
        [switch]$DuplicateEntry,
        [string]$MinimumVersion = '10.0.19041.0',
        [string]$Capability = 'runFullTrust'
    )
    $path = Join-Path $testRoot "$Name.msix"
    $stream = [IO.MemoryStream]::new([byte[]]::new(512), $true)
    $writer = [IO.BinaryWriter]::new($stream)
    $writer.Write([uint16]0x5A4D)
    $stream.Position = 0x3C
    $writer.Write([int]128)
    $stream.Position = 128
    $writer.Write([uint32]0x4550)
    $writer.Write([uint16]0x8664)
    $stream.Position = 152
    $writer.Write([uint16]0x20B)
    if ($Managed) { $stream.Position = 152 + 112 + 14 * 8; $writer.Write([uint32]1) }
    $exeBytes = if ($Truncated) { [byte[]]::new(3) } else { $stream.ToArray() }
    $writer.Dispose()
    $stream.Dispose()
    $manifest = @"
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10" xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10" xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities">
<Identity Name="Fixture.Production" Publisher="CN=Fixture" Version="1.13.96.0" ProcessorArchitecture="x64" />
<Properties><DisplayName>Fixture</DisplayName></Properties>
<Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="$MinimumVersion" MaxVersionTested="10.0.26100.0" /></Dependencies>
<Applications><Application Id="App"><uap:VisualElements DisplayName="Fixture" /><Extensions><uap:Extension Category="windows.protocol"><uap:Protocol Name="ncalc" /></uap:Extension></Extensions></Application></Applications>
<Capabilities><rescap:Capability Name="$Capability" /></Capabilities>
</Package>
"@
    $entries = @{
        'AppxManifest.xml' = [Text.Encoding]::UTF8.GetBytes($manifest)
        'NaturplayCalculator.exe' = $exeBytes
        'resources.pri' = [byte[]]::new(0)
    }
    if (-not $MissingRuntime) { $entries['Microsoft.UI.Xaml.dll'] = [byte[]]::new(0) }
    if ($UnsafeEntry) { $entries['../escape.txt'] = [byte[]]::new(0) }
    $zip = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in $entries.Keys) {
            $entry = $zip.CreateEntry($name).Open()
            try { $entry.Write($entries[$name], 0, $entries[$name].Length) } finally { $entry.Dispose() }
        }
        if ($DuplicateEntry) { $null = $zip.CreateEntry('RESOURCES.PRI') }
    } finally { $zip.Dispose() }
    return $path
}

try {
    $native = Join-Path $testRoot 'native.ps1'
    [IO.File]::WriteAllText($native, "Write-Output 'child output'; exit 23")
    Test-Case 'Native nonzero exit survives output logging' {
        $log = Join-Path $testRoot 'native.log'
        Assert-Failure { Invoke-CheckedNative pwsh @('-NoProfile', '-File', $native) -LogPath $log } '*exit code 23*'
        Assert-True ((Get-Content $log -Raw) -match 'child output') 'Native output was not logged.'
    }
    Test-Case 'Native successful exit is accepted' { Invoke-CheckedNative pwsh @('-NoProfile', '-Command', 'exit 0') }
    Test-Case 'Normal CoreCLR Debug runtime is accepted' {
        $runtimeConfig = Join-Path $testRoot 'debug.runtimeconfig.json'
        [IO.File]::WriteAllText($runtimeConfig, '{"runtimeOptions":{"configProperties":{}}}')
        Assert-DebugRuntime -RuntimeConfigPath $runtimeConfig
    }
    Test-Case 'AOT runtime restrictions are rejected in Debug output' {
        $runtimeConfig = Join-Path $testRoot 'restricted.runtimeconfig.json'
        foreach ($feature in @('System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported', 'System.Runtime.InteropServices.BuiltInComInterop.IsSupported')) {
            @{ runtimeOptions = @{ configProperties = @{ $feature = $false } } } | ConvertTo-Json -Depth 5 | Set-Content $runtimeConfig
            Assert-Failure { Assert-DebugRuntime -RuntimeConfigPath $runtimeConfig } '*Debug runtime disables*'
        }
    }
    Test-Case 'Contradictory switches fail from another working directory' {
        Push-Location $testRoot
        try { Assert-Failure { & (Join-Path $RepositoryRoot 'scripts/check.ps1') -CoreOnly -SkipTests } '*cannot be combined*' }
        finally { Pop-Location }
    }
    Test-Case 'CI cannot regenerate dependency locks' {
        $previous = $env:GITHUB_ACTIONS
        try {
            $env:GITHUB_ACTIONS = 'true'
            Assert-Failure { & (Join-Path $PSScriptRoot 'check.ps1') -CoreOnly -UpdateLock } '*CI cannot update*'
        } finally { $env:GITHUB_ACTIONS = $previous }
    }
    $validator = Join-Path $PSScriptRoot 'check-package.ps1'
    $preview = Join-Path $PSScriptRoot 'preview.ps1'
    $valid = New-PackageFixture valid
    Test-Case 'Valid Native AOT package structure is accepted' { & $validator -PackagePath $valid -Platform x64 }
    Test-Case 'Wrong architecture is rejected' { Assert-Failure { & $validator -PackagePath $valid -Platform x86 } '*architecture does not match*' }
    Test-Case 'Managed CLR executable is rejected' {
        $fixture = New-PackageFixture managed -Managed
        Assert-Failure { & $validator -PackagePath $fixture -Platform x64 } '*managed CLR header*'
    }
    Test-Case 'Truncated executable is rejected' {
        $fixture = New-PackageFixture truncated -Truncated
        Assert-Failure { & $validator -PackagePath $fixture -Platform x64 } '*Truncated PE*'
    }
    Test-Case 'Missing WinUI runtime is rejected' {
        $fixture = New-PackageFixture runtime -MissingRuntime
        Assert-Failure { & $validator -PackagePath $fixture -Platform x64 } '*missing Microsoft.UI.Xaml.dll*'
    }
    Test-Case 'Unexpected minimum OS version is rejected' {
        $fixture = New-PackageFixture minimum -MinimumVersion '10.0.26100.0'
        Assert-Failure { & $validator -PackagePath $fixture -Platform x64 } '*minimum version*'
    }
    Test-Case 'Unexpected capability is rejected' {
        $fixture = New-PackageFixture capability -Capability 'microphone'
        Assert-Failure { & $validator -PackagePath $fixture -Platform x64 } '*Unexpected package capabilities*'
    }
    Test-Case 'Case-insensitive duplicate archive names are rejected' {
        $fixture = New-PackageFixture duplicate -DuplicateEntry
        Assert-Failure { & $validator -PackagePath $fixture -Platform x64 } '*Duplicate package entry*'
    }
    Test-Case 'Preview rejects archive path traversal before extraction' {
        $fixture = New-PackageFixture traversal -UnsafeEntry
        $destination = Join-Path $testRoot 'unsafe-preview'
        Assert-Failure { & $preview -PackagePath $fixture -Destination $destination -Platform x64 } '*Unsafe package entry*'
        Assert-True (-not (Test-Path $destination)) 'Invalid preview created an output directory.'
    }
    Test-Case 'Preview WhatIf has no filesystem side effects' {
        $destination = Join-Path $testRoot 'whatif-preview'
        & $preview -PackagePath $valid -Destination $destination -Platform x64 -WhatIf
        Assert-True (-not (Test-Path $destination)) 'WhatIf created an output directory.'
    }
    Test-Case 'Preview preparation isolates identity and removes the production protocol' {
        $destination = Join-Path $testRoot 'preview'
        & $preview -PackagePath $valid -Destination $destination -Platform x64
        [xml]$manifest = Get-Content (Join-Path $destination 'AppxManifest.xml') -Raw
        Assert-True ($manifest.Package.Identity.Name -eq 'NaturplayCalculator.WinUI3.Preview') 'Preview identity was not isolated.'
        Assert-True ($manifest.Package.Identity.Version -eq '1.13.96.1') 'Preview version was not incremented.'
        Assert-True ($null -eq $manifest.SelectSingleNode("//*[local-name()='Extension' and @Category='windows.protocol']")) 'Production protocol was retained.'
        Assert-Failure { & $preview -PackagePath $valid -Destination $destination -Platform x64 } '*existing preview files are never overwritten*'
    }
    Write-Host "All $script:Passed script tests passed. No app was registered."
} finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $allowed = [IO.Path]::GetFullPath($testParent) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Refusing cleanup outside script test directory.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
