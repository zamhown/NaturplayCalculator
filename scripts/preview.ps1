#Requires -Version 7.2
<#
.SYNOPSIS
Prepares an isolated WinUI preview from a verified Release MSIX. Registration is opt-in.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$PackagePath,
    [Parameter(Mandatory)][string]$Destination,
    [Parameter(Mandatory)][ValidateSet('x64', 'x86', 'ARM64')][string]$Platform,
    [version]$Version,
    [switch]$Register,
    [switch]$ClosePreview
)
. (Join-Path $PSScriptRoot 'common.ps1')
if ($ClosePreview -and -not $Register) { throw 'ClosePreview requires Register.' }
if ($Register -and (-not $IsWindows -or -not (Get-Command Add-AppxPackage -ErrorAction SilentlyContinue))) {
    throw 'Registration requires Windows and the Add-AppxPackage command.'
}
if (-not [IO.Path]::IsPathRooted($Destination)) { throw 'Destination must be an absolute physical path visible to the deployment service.' }
$previewRoot = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $previewRoot) { throw 'Choose a new destination; existing preview files are never overwritten.' }
$package = (Resolve-Path -LiteralPath $PackagePath).Path
& (Join-Path $PSScriptRoot 'check-package.ps1') -PackagePath $package -Platform $Platform
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $reader = [IO.StreamReader]::new($archive.GetEntry('AppxManifest.xml').Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
} finally { $archive.Dispose() }
$identity = 'NaturplayCalculator.WinUI3.Preview'
$installed = if ($Register) { Get-AppxPackage -Name $identity } else { $null }
if (-not $Version) {
    $current = [version]$manifest.Package.Identity.Version
    if ($installed -and [version]$installed.Version -gt $current) { $current = [version]$installed.Version }
    $Version = [version]::new($current.Major, $current.Minor, $current.Build, $current.Revision + 1)
}
if ($Version.Revision -lt 0 -or $Version.Revision -gt 65535 -or $Version.Major -gt 65535 -or $Version.Minor -gt 65535 -or $Version.Build -gt 65535) { throw 'MSIX requires a four-part version with components from 0 to 65535.' }
if ($installed -and $Version -le [version]$installed.Version) { throw 'Preview version must be newer than the installed preview.' }
if (-not $PSCmdlet.ShouldProcess($previewRoot, 'Extract Release MSIX and prepare isolated preview')) { return }
[IO.Compression.ZipFile]::ExtractToDirectory($package, $previewRoot)
$manifestPath = Join-Path $previewRoot 'AppxManifest.xml'
$manifest.Package.Identity.SetAttribute('Name', $identity)
$manifest.Package.Identity.SetAttribute('Version', $Version.ToString())
$manifest.SelectSingleNode("//*[local-name()='Properties']/*[local-name()='DisplayName']").InnerText = 'Naturplay WinUI3 Preview'
foreach ($visual in $manifest.SelectNodes("//*[local-name()='VisualElements']")) { $visual.SetAttribute('DisplayName', 'Naturplay WinUI3 Preview') }
foreach ($extension in @($manifest.SelectNodes("//*[local-name()='Extension' and @Category='windows.protocol']"))) {
    $extension.ParentNode.RemoveChild($extension) | Out-Null
}
$manifest.Save($manifestPath)
# The extracted layout is registered as development files, never redistributed as a signed package.
foreach ($metadata in @('AppxSignature.p7x', 'AppxBlockMap.xml', '[Content_Types].xml')) {
    $metadataPath = Join-Path $previewRoot $metadata
    if (Test-Path -LiteralPath $metadataPath) { Remove-Item -LiteralPath $metadataPath }
}
Write-Host "Prepared isolated preview: $manifestPath"
if ($Register -and $PSCmdlet.ShouldProcess($manifestPath, 'Register isolated preview package')) {
    Add-AppxPackage -Register $manifestPath -ForceApplicationShutdown:$ClosePreview
    Get-AppxPackage -Name $identity | Select-Object Name, Version, InstallLocation
}
