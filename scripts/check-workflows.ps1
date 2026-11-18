#Requires -Version 7.2
<#
.SYNOPSIS
Downloads a checksum-verified actionlint release and validates GitHub workflows.
#>
[CmdletBinding()]
param()
. (Join-Path $PSScriptRoot 'common.ps1')
$tool = (Get-Content (Join-Path $PSScriptRoot 'tools.json') -Raw | ConvertFrom-Json -AsHashtable).actionlint
$os = if ($IsWindows) { 'windows' } elseif ($IsMacOS) { 'darwin' } else { 'linux' }
$architecture = switch ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()) {
    'X64' { 'amd64' }
    'Arm64' { 'arm64' }
    default { throw 'actionlint requires an x64 or ARM64 PowerShell host.' }
}
$key = "${os}_$architecture"
if (-not $tool.sha256.ContainsKey($key)) { throw "No verified actionlint binary for $key." }
$extension = if ($IsWindows) { 'zip' } else { 'tar.gz' }
$directory = Join-Path $RepositoryRoot "artifacts/tools/actionlint/$($tool.version)/$key"
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$archive = Join-Path $directory "actionlint.$extension"
$uri = "https://github.com/rhysd/actionlint/releases/download/v$($tool.version)/actionlint_$($tool.version)_$key.$extension"
if (-not (Test-Path -LiteralPath $archive)) { Invoke-WebRequest -Uri $uri -OutFile $archive }
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $tool.sha256[$key]) {
    throw "actionlint checksum mismatch. Remove '$archive' and retry."
}
if ($IsWindows) {
    Expand-Archive -LiteralPath $archive -DestinationPath $directory -Force
} else {
    Assert-Command tar
    Invoke-CheckedNative tar @('-xzf', $archive, '-C', $directory, 'actionlint')
}
$executable = Join-Path $directory $(if ($IsWindows) { 'actionlint.exe' } else { 'actionlint' })
Push-Location $RepositoryRoot
try {
    # Disable optional external linters so this check is identical on all hosts.
    Invoke-CheckedNative $executable @('-color', '-shellcheck=', '-pyflakes=')
    Write-Host "Workflow validation passed (actionlint $($tool.version))."
}
finally { Pop-Location }
