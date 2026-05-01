<#
.SYNOPSIS
    Build a self-contained Windows release artifact for RocketLeagueStats.

.DESCRIPTION
    Publishes the console app as a single-file, self-contained win-x64
    executable, packages it into a versioned zip alongside a SHA256 checksum
    file, and prints a ready-to-run `gh release create` command.

.PARAMETER Version
    Semantic version for the release (e.g. 1.0.0, 1.2.3-rc1). Used in the
    zip filename and embedded into the assembly via -p:Version=...

.PARAMETER Configuration
    Build configuration. Defaults to Release. Changing this is rarely useful
    for an actual release.

.PARAMETER Runtime
    .NET runtime identifier. Defaults to win-x64. The app is Windows-only
    (Rocket League runs on Windows only), so other RIDs are not supported.

.PARAMETER SkipTests
    Skip 'dotnet test' before publishing. Off by default — release builds
    run tests as a sanity gate. Use only when you've already verified.

.PARAMETER KeepSymbols
    Keep .pdb and .xml files in the zip. Off by default; release zips ship
    clean for size. Pass this if you want symbols for crash diagnosis.

.EXAMPLE
    pwsh ./tools/Build-Release.ps1 -Version 1.0.0

.EXAMPLE
    pwsh ./tools/Build-Release.ps1 -Version 1.1.0-rc1 -SkipTests
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$')]
    [string]$Version,

    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [switch]$SkipTests,
    [switch]$KeepSymbols
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$projectPath  = 'src/RocketLeagueStats.Console/RocketLeagueStats.Console.csproj'
$artifactName = "RocketLeagueStats-v$Version-$Runtime"
$publishDir   = "artifacts/$artifactName"
$zipPath      = "artifacts/$artifactName.zip"
$shaPath      = "$zipPath.sha256"

# AssemblyVersion / FileVersion must be 4-part numeric — strip any prerelease
# suffix (e.g. "1.2.3-rc1" -> "1.2.3.0") before passing to MSBuild.
$numericVersion = ($Version -replace '-.*$', '') + '.0'

Write-Host "Building RocketLeagueStats v$Version ($Runtime)" -ForegroundColor Cyan

if (-not $SkipTests) {
    Write-Host "`n[1/4] Running tests..." -ForegroundColor Yellow
    dotnet test -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed. Aborting release. Re-run with -SkipTests to override (not recommended)."
    }
} else {
    Write-Host "`n[1/4] Skipping tests (-SkipTests)" -ForegroundColor DarkYellow
}

Write-Host "`n[2/4] Cleaning previous output for v$Version..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path 'artifacts' | Out-Null
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
if (Test-Path $zipPath)    { Remove-Item -Force $zipPath }
if (Test-Path $shaPath)    { Remove-Item -Force $shaPath }

Write-Host "`n[3/4] Publishing..." -ForegroundColor Yellow
dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    -p:SelfContained=false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version `
    -p:AssemblyVersion=$numericVersion `
    -p:FileVersion=$numericVersion `
    -p:InformationalVersion=$Version `
    -o $publishDir `
    --nologo

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

if (-not $KeepSymbols) {
    Get-ChildItem $publishDir -Include *.pdb, *.xml -File -Recurse | Remove-Item -Force
}

Write-Host "`n[4/4] Packaging..." -ForegroundColor Yellow
# Zip the directory itself (not its contents) so users unzip into a folder
# rather than having files spill into their Downloads dir.
Compress-Archive -Path $publishDir -DestinationPath $zipPath -Force

$hash = (Get-FileHash -Algorithm SHA256 $zipPath).Hash.ToLower()
# sha256sum-compatible format ('*' marks binary mode); enables `sha256sum -c file.zip.sha256`.
"$hash *$(Split-Path -Leaf $zipPath)" | Set-Content -Path $shaPath -Encoding ascii

$zipSizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host "`nRelease artifact ready:" -ForegroundColor Green
Write-Host "  Zip:      $zipPath ($zipSizeMB MB)"
Write-Host "  SHA256:   $hash"
Write-Host "  Hashfile: $shaPath"
Write-Host ""
Write-Host "Upload to GitHub Releases:" -ForegroundColor Cyan
Write-Host "  gh release create v$Version $zipPath $shaPath ``"
Write-Host "    --title 'v$Version' ``"
Write-Host "    --notes-file <path-to-release-notes.md>"
