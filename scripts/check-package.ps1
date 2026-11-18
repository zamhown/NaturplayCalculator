#Requires -Version 7.2
<#
.SYNOPSIS
Validates Release MSIX contents, architecture, Native AOT and manifest constraints.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][ValidateSet('x64', 'x86', 'ARM64')][string]$Platform
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath).Path)
try {
    $entries = @($archive.Entries.FullName)
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $entries) {
        if (-not $seen.Add($entry)) { throw "Duplicate package entry: $entry" }
        if ($entry -match '(^[/\\]|^[a-zA-Z]:|(^|[/\\])\.\.([/\\]|$))') { throw "Unsafe package entry: $entry" }
    }
    foreach ($required in @('NaturplayCalculator.exe', 'Microsoft.UI.Xaml.dll', 'resources.pri', 'AppxManifest.xml')) {
        if ($required -notin $entries) { throw "Package is missing $required." }
    }
    foreach ($forbidden in @('coreclr.dll', 'NaturplayCalculator.dll', 'Commands.xml')) {
        if ($forbidden -in $entries) { throw "Release package contains unexpected $forbidden." }
    }
    $reader = [System.IO.StreamReader]::new($archive.GetEntry('AppxManifest.xml').Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($manifest.Package.Identity.ProcessorArchitecture -ne $Platform.ToLowerInvariant()) {
        throw "Package manifest architecture does not match $Platform."
    }
    $family = $manifest.Package.Dependencies.TargetDeviceFamily
    if ($family.Name -ne 'Windows.Desktop' -or $family.MinVersion -ne '10.0.19041.0') {
        throw 'Package must retain Windows Desktop minimum version 10.0.19041.0.'
    }
    $capabilities = @($manifest.Package.Capabilities.ChildNodes | Where-Object NodeType -eq Element | ForEach-Object { $_.GetAttribute('Name') })
    if ($capabilities.Count -ne 1 -or $capabilities[0] -ne 'runFullTrust') {
        throw 'Unexpected package capabilities; review desktop file picker access before adding library or device capabilities.'
    }
    if ($manifest.SelectSingleNode("//*[local-name()='PhoneIdentity']")) { throw 'Legacy phone identity is still packaged.' }

    # Check the binary itself: a manifest alone cannot prove the compiler used the requested architecture.
    $exe = [System.IO.MemoryStream]::new()
    $source = $archive.GetEntry('NaturplayCalculator.exe').Open()
    try { $source.CopyTo($exe) } finally { $source.Dispose() }
    $binary = [System.IO.BinaryReader]::new($exe)
    try {
        $exe.Position = 0
        if ($exe.Length -lt 64) { throw 'Truncated PE executable.' }
        if ($binary.ReadUInt16() -ne 0x5A4D) { throw 'Application is not a PE executable.' }
        $exe.Position = 0x3C
        $peOffset = $binary.ReadInt32()
        if ($peOffset -lt 64 -or $peOffset -gt $exe.Length - 24) { throw 'Invalid PE header offset.' }
        $exe.Position = $peOffset
        if ($binary.ReadUInt32() -ne 0x4550) { throw 'Application has no PE signature.' }
        $machine = $binary.ReadUInt16()
        $expected = @{ x86 = 0x014C; x64 = 0x8664; ARM64 = 0xAA64 }[$Platform]
        if ($machine -ne $expected) { throw "Executable machine 0x$($machine.ToString('X4')) does not match $Platform." }
        $exe.Position = $peOffset + 24
        $magic = $binary.ReadUInt16()
        $directoryOffset = if ($magic -eq 0x20B) { 112 } elseif ($magic -eq 0x10B) { 96 } else { throw 'Unknown PE optional header.' }
        if ($peOffset + 24 + $directoryOffset + (15 * 8) -gt $exe.Length) { throw 'Truncated PE data directories.' }
        $exe.Position = $peOffset + 24 + $directoryOffset + (14 * 8)
        if ($binary.ReadUInt32() -ne 0 -or $binary.ReadUInt32() -ne 0) { throw 'Application has a managed CLR header; expected Native AOT.' }
    }
    finally { $binary.Dispose(); $exe.Dispose() }
    Write-Host "Verified $Platform Native AOT MSIX, Windows 10 19041 minimum, desktop capabilities: $PackagePath"
}
finally { $archive.Dispose() }
