$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$elevatedLauncher = Join-Path $root 'run-sniffer-elevated.ps1'

if (-not (Test-Path -LiteralPath $elevatedLauncher)) {
    throw "Missing $elevatedLauncher"
}

$repoDist = Join-Path $root 'dist'
$overlay = if (Test-Path -LiteralPath (Join-Path $root 'Overlay\RocksmithTabOverlay.exe')) {
    Join-Path $root 'Overlay\RocksmithTabOverlay.exe'
}
else {
    Join-Path $repoDist 'Overlay\RocksmithTabOverlay.exe'
}
if (-not (Test-Path -LiteralPath $overlay)) {
    throw "The overlay has not been built yet. Run .\build.ps1 first."
}

# Rocksmith normally runs elevated. The overlay must use the same integrity
# level to find and follow the game window, so the whole launcher is elevated.
Start-Process -FilePath 'powershell.exe' -Verb RunAs -WindowStyle Hidden -ArgumentList @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $elevatedLauncher + '"')
)
