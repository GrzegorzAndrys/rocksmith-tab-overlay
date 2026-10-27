param(
    [switch]$Deploy,
    [switch]$DeployOverlay,
    [switch]$RebuildCache,
    [string]$LogPath
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$defaultLogDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'RocksmithTabOverlay'
if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $defaultLogDirectory 'launcher.log'
}
$logDirectory = Split-Path -Parent $LogPath

function Write-LauncherLog([string]$message) {
    if ($logDirectory) {
        New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    }
    Add-Content -LiteralPath $LogPath -Value ('[{0:yyyy-MM-dd HH:mm:ss.fff}] {1}' -f (Get-Date), $message)
}

trap {
    try { Write-LauncherLog "ERROR: $($_.Exception.Message)" } catch { }
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}

Write-LauncherLog "Administrator launcher started from $scriptRoot"
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
    Write-LauncherLog "Starting RockSniffer: $sniffer"
    $snifferProcess = Start-Process -FilePath $sniffer -WorkingDirectory (Split-Path -Parent $sniffer) -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 700
    if ($snifferProcess.HasExited) {
        throw "RockSniffer closed immediately with exit code $($snifferProcess.ExitCode). Check whether antivirus blocked or quarantined it."
    }
}
else {
    Write-LauncherLog 'RockSniffer is already running.'
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

Write-LauncherLog "Starting overlay: $overlay"
$overlayProcess = Start-Process -FilePath $overlay -WorkingDirectory (Split-Path -Parent $overlay) -PassThru
Start-Sleep -Milliseconds 1000
if ($overlayProcess.HasExited) {
    throw "RocksmithTabOverlay closed immediately with exit code $($overlayProcess.ExitCode)."
}
$activeSniffer = Get-Process -Name RockSniffer | Select-Object -First 1
Write-LauncherLog "Started successfully (RockSniffer PID $($activeSniffer.Id), overlay PID $($overlayProcess.Id))."
