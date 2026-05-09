<#
.SYNOPSIS
    Build the Angular WebApp (with Workbox service worker) and copy the output
    to RocketLeagueStats.WebApi/wwwroot.

.DESCRIPTION
    Two-stage build: (1) `ng build` produces the Angular bundle in
    dist/<project>/browser; (2) `node ./tools/build-sw.mjs` bundles src/sw.ts
    via esbuild and runs Workbox `injectManifest` to write sw.js into the same
    folder with the precache manifest embedded.

    The SW step runs unconditionally. In a development-config build the SW is
    still emitted but main.ts's isDevMode() guard prevents runtime registration,
    so it sits inert in wwwroot.

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

    # Post-build: bundle the SW (esbuild) and embed the precache manifest
    # (workbox-build injectManifest). Must run AFTER ng build so the dist
    # folder exists for the manifest glob to walk.
    Write-Host "Bundling service worker (esbuild + injectManifest)..." -ForegroundColor Cyan
    & node ./tools/build-sw.mjs
    if ($LASTEXITCODE -ne 0) { throw "Service worker build failed (exit $LASTEXITCODE)" }
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

# Sanity check: build-sw.mjs is supposed to have written sw.js next to index.html.
# A missing sw.js means the post-build step silently no-op'd (e.g. WB_APP_NAME
# mismatch) - fail loudly here rather than ship a wwwroot that 404s /sw.js.
$swPath = Join-Path $dist 'sw.js'
if (-not (Test-Path $swPath)) {
    throw "Service worker missing at '$swPath'. The build-sw.mjs post-build step did not produce sw.js."
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
