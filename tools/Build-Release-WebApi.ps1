<#
.SYNOPSIS
    Build a Windows release artifact for the RocketLeagueStats Web Dashboard host (WebApi).
.DESCRIPTION
    Builds the Angular WebApp first (so wwwroot is populated), then publishes
    the WebApi as a single-file win-x64 executable. Defaults to a non-self-
    contained build (small ~6 MB EXE that requires .NET 10 runtime installed
    on the target machine). Pass -SelfContained for a runtime-bundled build
    (~80 MB EXE that runs on any Windows machine without any prerequisites).
.PARAMETER Version
    Semantic version. Used in the zip filename.
.PARAMETER Configuration
    Build configuration. Defaults to Release.
.PARAMETER Runtime
    .NET runtime identifier. Defaults to win-x64.
.PARAMETER SelfContained
    Bundle the .NET runtime into the EXE so it runs without .NET installed.
    Trade-off: ~80 MB zip instead of ~6 MB. Use this for end-user distribution
    where you can't assume the .NET 10 runtime is present.
.PARAMETER SkipTests
    Skip 'dotnet test' before publishing.
.PARAMETER KeepSymbols
    Keep .pdb and .xml in the zip.
.EXAMPLE
    pwsh ./tools/Build-Release-WebApi.ps1 -Version 1.5.0
.EXAMPLE
    pwsh ./tools/Build-Release-WebApi.ps1 -Version 1.5.0 -SelfContained
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$')]
    [string]$Version,

    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [switch]$SelfContained,
    [switch]$SkipTests,
    [switch]$KeepSymbols
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$projectPath  = 'src/RocketLeagueStats.WebApi/RocketLeagueStats.WebApi.csproj'
$artifactSuffix = if ($SelfContained) { '-self-contained' } else { '' }
$artifactName = "RocketLeagueStats-WebApi-v$Version-$Runtime$artifactSuffix"
$publishDir   = "artifacts/$artifactName"
$zipPath      = "artifacts/$artifactName.zip"
$shaPath      = "$zipPath.sha256"

$numericVersion = ($Version -replace '-.*$', '') + '.0'

Write-Host "Building RocketLeagueStats WebApi v$Version ($Runtime)" -ForegroundColor Cyan

if (-not $SkipTests) {
    Write-Host "`n[1/5] Running tests..." -ForegroundColor Yellow
    dotnet test -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
} else {
    Write-Host "`n[1/5] Skipping tests" -ForegroundColor DarkYellow
}

Write-Host "`n[2/5] Building Angular WebApp..." -ForegroundColor Yellow
& (Join-Path $PSScriptRoot 'Build-WebApp.ps1') -Configuration production
if ($LASTEXITCODE -ne 0) { throw "Angular build failed." }

Write-Host "`n[3/5] Cleaning previous output..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path 'artifacts' | Out-Null
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
if (Test-Path $zipPath)    { Remove-Item -Force $zipPath }
if (Test-Path $shaPath)    { Remove-Item -Force $shaPath }

$selfContainedFlag = if ($SelfContained) { 'true' } else { 'false' }
Write-Host "`n[4/5] Publishing (SelfContained=$selfContainedFlag)..." -ForegroundColor Yellow

# EnableCompressionInSingleFile is only valid for self-contained builds; skip it otherwise.
$publishArgs = @(
    'publish', $projectPath,
    '-c', $Configuration,
    '-r', $Runtime,
    "-p:SelfContained=$selfContainedFlag",
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    "-p:Version=$Version",
    "-p:AssemblyVersion=$numericVersion",
    "-p:FileVersion=$numericVersion",
    "-p:InformationalVersion=$Version",
    '-o', $publishDir,
    '--nologo'
)
if ($SelfContained) {
    $publishArgs += '-p:EnableCompressionInSingleFile=true'
}
dotnet @publishArgs

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

# Sanity check: SWA pipeline regressions can silently drop wwwroot from the
# published output (see csproj history). Fail loudly here rather than ship an
# EXE that 404s every static file.
$publishedIndex = Join-Path $publishDir 'wwwroot/index.html'
if (-not (Test-Path $publishedIndex)) {
    throw "Publish output missing wwwroot/index.html at '$publishedIndex'. The Angular bundle did not make it into the artifact."
}

if (-not $KeepSymbols) {
    Get-ChildItem $publishDir -Include *.pdb, *.xml -File -Recurse | Remove-Item -Force
}

Write-Host "`n[5/5] Packaging..." -ForegroundColor Yellow
Compress-Archive -Path $publishDir -DestinationPath $zipPath -Force

$hash = (Get-FileHash -Algorithm SHA256 $zipPath).Hash.ToLower()
"$hash *$(Split-Path -Leaf $zipPath)" | Set-Content -Path $shaPath -Encoding ascii

$zipSizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host "`nWebApi release artifact ready:" -ForegroundColor Green
Write-Host "  Zip:      $zipPath ($zipSizeMB MB)"
Write-Host "  SHA256:   $hash"
Write-Host "  Hashfile: $shaPath"
Write-Host ""
Write-Host "Upload to GitHub Releases:" -ForegroundColor Cyan
Write-Host "  gh release create v$Version-dashboard $zipPath $shaPath ``"
Write-Host "    --title 'v$Version Web Dashboard' ``"
Write-Host "    --notes-file <path-to-release-notes.md>"
