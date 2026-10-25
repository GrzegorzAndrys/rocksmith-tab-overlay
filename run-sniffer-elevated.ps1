param(
    [switch]$Deploy,
    [switch]$DeployOverlay,
    [switch]$RebuildCache
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = @(
    (Join-Path $scriptRoot 'dist'),
    $scriptRoot
) | Where-Object {
    (Test-Path -LiteralPath (Join-Path $_ 'RockSniffer\RockSniffer.exe')) -and
    (Test-Path -LiteralPath (Join-Path $_ 'Overlay\RocksmithTabOverlay.exe'))
} | Select-Object -First 1

if (-not $dist) {
    throw "Could not find RockSniffer and Overlay next to $scriptRoot. Run .\build.ps1 first."
}

$sniffer = Join-Path $dist 'RockSniffer\RockSniffer.exe'
$overlay = Join-Path $dist 'Overlay\RocksmithTabOverlay.exe'
$staged = Join-Path $scriptRoot 'dist-staging\RockSniffer'
$stagedOverlay = Join-Path $scriptRoot 'dist-next\Overlay'

function Copy-StagedFiles([string]$source, [string]$destination) {
    for ($attempt = 1; $attempt -le 12; $attempt++) {
        try {
            Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse -Force
            return
        }
        catch {
            if ($attempt -eq 12) { throw }
            Start-Sleep -Milliseconds 300
        }
    }
}

$liveSnifferDll = Join-Path (Split-Path -Parent $sniffer) 'RockSniffer.dll'
$stagedSnifferDll = Join-Path $staged 'RockSniffer.dll'
$newerSnifferIsStaged = (Test-Path -LiteralPath $stagedSnifferDll) -and
    (!(Test-Path -LiteralPath $liveSnifferDll) -or
     (Get-Item -LiteralPath $stagedSnifferDll).LastWriteTimeUtc -gt (Get-Item -LiteralPath $liveSnifferDll).LastWriteTimeUtc)
if ($Deploy -or $newerSnifferIsStaged) {
    if (-not (Test-Path -LiteralPath $staged)) { throw "No staged RockSniffer build at $staged" }
    $runningSniffers = @(Get-Process -Name RockSniffer -ErrorAction SilentlyContinue)
    $runningSniffers | Stop-Process -Force
    $runningSniffers | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue
    Copy-StagedFiles $staged (Split-Path -Parent $sniffer)
}

if ($RebuildCache) {
    $cache = Join-Path (Split-Path -Parent $sniffer) 'cache.sqlite'
    $backup = Join-Path (Split-Path -Parent $sniffer) 'cache.pre-phrase-fix.sqlite'
    if ((Test-Path -LiteralPath $cache) -and !(Test-Path -LiteralPath $backup)) {
        Move-Item -LiteralPath $cache -Destination $backup
    }
}

if (-not (Get-Process -Name RockSniffer -ErrorAction SilentlyContinue)) {
    Start-Process -FilePath $sniffer -WorkingDirectory (Split-Path -Parent $sniffer) -WindowStyle Hidden
}

# Rocksmith normally runs elevated on this installation. The overlay must run at
# the same integrity level to locate and follow the game window.
$runningOverlays = @(Get-Process -Name RocksmithTabOverlay -ErrorAction SilentlyContinue)
$runningOverlays | Stop-Process -Force
$runningOverlays | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue

# Development convenience: a build placed in dist-next is picked up without a
# separate deploy step. Release installs simply do not have that folder.
$liveOverlayDll = Join-Path (Split-Path -Parent $overlay) 'RocksmithTabOverlay.dll'
$stagedOverlayDll = Join-Path $stagedOverlay 'RocksmithTabOverlay.dll'
$newerOverlayIsStaged = (Test-Path -LiteralPath $stagedOverlayDll) -and
    (!(Test-Path -LiteralPath $liveOverlayDll) -or
     (Get-Item -LiteralPath $stagedOverlayDll).LastWriteTimeUtc -gt (Get-Item -LiteralPath $liveOverlayDll).LastWriteTimeUtc)
if ($DeployOverlay -or $newerOverlayIsStaged) {
    if (-not (Test-Path -LiteralPath $stagedOverlay)) { throw "No staged overlay build at $stagedOverlay" }
    Copy-StagedFiles $stagedOverlay (Split-Path -Parent $overlay)
}

Start-Process -FilePath $overlay -WorkingDirectory (Split-Path -Parent $overlay)
