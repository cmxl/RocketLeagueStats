<#
.SYNOPSIS
    Build the Angular WebApp and copy the output to RocketLeagueStats.WebApi/wwwroot.

.PARAMETER Configuration
    Build configuration. Defaults to 'production'. Use 'development' for faster
    builds during local iteration.
#>

[CmdletBinding()]
param(
    [string]$Configuration = 'production'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$webApp = Join-Path $repoRoot 'src/RocketLeagueStats.WebApp'
$webProject = Join-Path $repoRoot 'src/RocketLeagueStats.WebApi'
$wwwroot = Join-Path $webProject 'wwwroot'

# Locate the build output directory (Angular 21 default: dist/<app-name>/browser)
$distSearchRoot = Join-Path $webApp 'dist'

Write-Host "Building Angular WebApp ($Configuration)..." -ForegroundColor Cyan

Push-Location $webApp
try {
    & npm ci
    if ($LASTEXITCODE -ne 0) { throw "npm ci failed (exit $LASTEXITCODE)" }

    & npx ng build --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw "ng build failed (exit $LASTEXITCODE)" }
} finally {
    Pop-Location
}

# Resolve the actual browser bundle path. Angular 21 emits at dist/<project>/browser/.
$dist = Get-ChildItem -Path $distSearchRoot -Directory | ForEach-Object {
    $candidate = Join-Path $_.FullName 'browser'
    if (Test-Path $candidate) { $candidate }
} | Select-Object -First 1

if (-not $dist) {
    throw "Could not locate Angular browser bundle under $distSearchRoot. Expected dist/<project>/browser/"
}

Write-Host "Copying $dist -> $wwwroot" -ForegroundColor Cyan

if (Test-Path $wwwroot) {
    # Clear existing contents but preserve .gitkeep
    Get-ChildItem -Path $wwwroot -Force -Exclude '.gitkeep' | Remove-Item -Recurse -Force
} else {
    New-Item -ItemType Directory -Path $wwwroot -Force | Out-Null
}

Copy-Item -Path (Join-Path $dist '*') -Destination $wwwroot -Recurse -Force

Write-Host "Build-WebApp complete. Bundle deployed to wwwroot." -ForegroundColor Green
