#Requires -Version 7.2
<#
.SYNOPSIS
Checks repository files, PowerShell syntax, project references, locks and local links.
#>
[CmdletBinding()]
param()
. (Join-Path $PSScriptRoot 'common.ps1')
Assert-Command git
Push-Location $RepositoryRoot
try {
    $required = @('README.md', 'LICENSE', 'CONTRIBUTING.md', 'SECURITY.md', '.editorconfig', '.gitattributes', 'NuGet.Config', 'global.json', '.github/workflows/ci.yml', '.github/dependabot.yml', '.github/PULL_REQUEST_TEMPLATE.md', 'docs/development.md', 'math/README.md', 'ncalc/NaturplayCalculator.sln')
    foreach ($path in $required) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required repository file is missing: $path" }
    }
    $paths = @(& git -c core.quotepath=false ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate repository files.' }
    $paths = @($paths | Sort-Object -Unique -CaseSensitive | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($path in $paths) {
        if (-not $names.Add($path)) { throw "Case-colliding repository path: $path" }
        if ($path -match '(^|/)(bin|obj|AppPackages|TestResults|artifacts)/|\.(pfx|p12|suo|user|binlog|trx)$') {
            throw "Generated output or credentials must not be tracked: $path"
        }
        if ($path.EndsWith('.ps1')) {
            $tokens = $null
            $parseErrors = $null
            $null = [Management.Automation.Language.Parser]::ParseFile((Join-Path $RepositoryRoot $path), [ref]$tokens, [ref]$parseErrors)
            if ($parseErrors.Count) { throw "PowerShell syntax error in ${path}: $($parseErrors.Message -join '; ')" }
        }
        if ($path.EndsWith('.json')) { $null = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json }
        if ($path -like 'ncalc/*.csproj' -or $path -like 'math/*.csproj') {
            [xml]$project = Get-Content -LiteralPath $path -Raw
            foreach ($reference in $project.SelectNodes('//ProjectReference')) {
                $relative = $reference.Include.Replace('\', '/')
                if (-not (Test-Path -LiteralPath (Join-Path (Split-Path $path) $relative))) { throw "Broken project reference in ${path}: $relative" }
            }
            if (-not (Test-Path -LiteralPath (Join-Path (Split-Path $path) 'packages.lock.json'))) { throw "Missing dependency lock for $path" }
        }
        if ($path.EndsWith('.md')) {
            $content = Get-Content -LiteralPath $path -Raw
            foreach ($link in [regex]::Matches($content, '\[[^\]]*\]\(([^)\s]+)\)')) {
                $target = $link.Groups[1].Value
                if ($target -match '^(?:[a-z][a-z0-9+.-]*:|#)') { continue }
                $target = [Uri]::UnescapeDataString(($target -split '#', 2)[0])
                $base = if ($path.Contains('/')) { Split-Path $path } else { '.' }
                if (-not (Test-Path -LiteralPath (Join-Path $base $target))) { throw "Broken Markdown link in ${path}: $target" }
            }
        }
        if ($path -like '.github/workflows/*.yml') {
            foreach ($line in (Get-Content -LiteralPath $path)) {
                if ($line -match '^\s*-?\s*uses:\s*([^\s#]+)' -and $Matches[1] -notmatch '^\./|@[a-f0-9]{40}$') {
                    throw "Action must be pinned to a full commit SHA: $line"
                }
            }
        }
    }
    foreach ($solution in @('ncalc/NaturplayCalculator.sln', 'math/NaturplayMath.sln')) {
        foreach ($line in (Get-Content -LiteralPath $solution)) {
            if ($line -match '^Project\([^)]*\) = "[^"]+", "([^"]+\.csproj)"') {
                $projectPath = Join-Path (Split-Path $solution) $Matches[1].Replace('\', '/')
                if (-not (Test-Path -LiteralPath $projectPath)) { throw "Broken solution project in ${solution}: $line" }
            }
        }
    }
    Write-Host "Repository checks passed ($($paths.Count) source/configuration files)."
}
finally { Pop-Location }
