param(
    [Parameter(Mandatory = $true)][string]$Destination,
    [version]$Version,
    [switch]$ClosePreview
)
$ErrorActionPreference = 'Stop'
$project = Join-Path (Split-Path $PSScriptRoot) 'NaturplayCalculator'
$recipePath = Join-Path $project 'bin\x64\Release\NaturplayCalculator.build.appxrecipe'
if (-not (Test-Path -LiteralPath $recipePath)) { throw 'Build x64 Release before registering a preview.' }
if (-not [IO.Path]::IsPathRooted($Destination)) { throw 'Destination must be an absolute physical path visible to the deployment service.' }
$previewRoot = [IO.Path]::GetFullPath($Destination).TrimEnd('\')
if (Test-Path -LiteralPath $previewRoot) { throw 'Choose a new destination; existing preview files are never overwritten.' }
$installed = Get-AppxPackage -Name NaturplayCalculator.SymbolicPreview
if (-not $Version) {
    $current = if ($installed) { [version]$installed.Version } else { [version]'1.13.96.0' }
    $Version = [version]::new($current.Major, $current.Minor, $current.Build, $current.Revision + 1)
}
if ($installed -and $Version -le [version]$installed.Version) { throw 'Preview version must be newer than the installed preview.' }
[xml]$recipe = Get-Content -LiteralPath $recipePath -Raw
$namespaces = [System.Xml.XmlNamespaceManager]::new($recipe.NameTable)
$namespaces.AddNamespace('m', 'http://schemas.microsoft.com/developer/msbuild/2003')
foreach ($file in $recipe.SelectNodes('//m:AppxPackagedFile | //m:AppXManifest', $namespaces)) {
    $target = [IO.Path]::GetFullPath((Join-Path $previewRoot $file.PackagePath))
    if (-not $target.StartsWith($previewRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Recipe path escapes the preview destination.' }
    $source = [string]$file.Include
    if (-not [IO.Path]::IsPathRooted($source)) { $source = Join-Path $project $source }
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
}
$manifestPath = Join-Path $previewRoot 'AppxManifest.xml'
[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
$manifest.Package.Identity.SetAttribute('Name', 'NaturplayCalculator.SymbolicPreview')
$manifest.Package.Identity.SetAttribute('Version', $Version.ToString())
$manifest.SelectSingleNode("//*[local-name()='Properties']/*[local-name()='DisplayName']").InnerText = 'Naturplay Preview'
foreach ($visual in $manifest.SelectNodes("//*[local-name()='VisualElements']")) { $visual.SetAttribute('DisplayName', 'Naturplay Preview') }
foreach ($phone in $manifest.SelectNodes("//*[local-name()='PhoneIdentity']")) { $phone.SetAttribute('PhoneProductId', [guid]::NewGuid().ToString()) }
foreach ($extension in @($manifest.SelectNodes("//*[local-name()='Extension' and @Category='windows.protocol']"))) {
    $extension.ParentNode.RemoveChild($extension) | Out-Null
}
$manifest.Save($manifestPath)
# This is a development file registration, not an already staged store package.
Add-AppxPackage -Register $manifestPath -ForceApplicationShutdown:$ClosePreview
Get-AppxPackage -Name NaturplayCalculator.SymbolicPreview | Select-Object Name, Version, InstallLocation
