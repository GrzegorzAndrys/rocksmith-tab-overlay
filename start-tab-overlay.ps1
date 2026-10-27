[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$elevatedLauncher = Join-Path $root 'run-sniffer-elevated.ps1'
$logDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'RocksmithTabOverlay'
$logPath = Join-Path $logDirectory 'launcher.log'

function Write-LauncherLog([string]$message) {
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    Add-Content -LiteralPath $logPath -Value ('[{0:yyyy-MM-dd HH:mm:ss.fff}] {1}' -f (Get-Date), $message)
}

function Show-LaunchError([string]$message) {
    try {
        Add-Type -AssemblyName PresentationFramework
        [System.Windows.MessageBox]::Show(
            $message,
            'Rocksmith TAB Overlay could not start',
            [System.Windows.MessageBoxButton]::OK,
            [System.Windows.MessageBoxImage]::Error
        ) | Out-Null
    }
    catch {
        # The console and log still contain the failure if WPF is unavailable.
    }
}

try {
    if (-not (Test-Path -LiteralPath $elevatedLauncher)) {
        throw "Missing launcher file: $elevatedLauncher. Extract the complete release ZIP before running it."
    }

    $dist = @(
        $root,
        (Join-Path $root 'dist')
    ) | Where-Object {
        (Test-Path -LiteralPath (Join-Path $_ 'RockSniffer\RockSniffer.exe')) -and
        (Test-Path -LiteralPath (Join-Path $_ 'Overlay\RocksmithTabOverlay.exe'))
    } | Select-Object -First 1

    if (-not $dist) {
        throw "RockSniffer or RocksmithTabOverlay.exe is missing. Extract the complete release ZIP instead of copying only the launcher files. Antivirus quarantine can also remove RockSniffer.exe."
    }

    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    Set-Content -LiteralPath $logPath -Value ('[{0:yyyy-MM-dd HH:mm:ss.fff}] Starting from {1}' -f (Get-Date), $root)

    # Rocksmith commonly runs elevated. Run both companion processes at the
    # same integrity level, but wait for this short bootstrapper so failures can
    # be surfaced instead of disappearing in a hidden administrator window.
    $elevatedProcess = Start-Process -FilePath 'powershell.exe' -Verb RunAs -WindowStyle Hidden -Wait -PassThru -ArgumentList @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', ('"' + $elevatedLauncher + '"'),
        '-LogPath', ('"' + $logPath + '"')
    )

    if ($elevatedProcess.ExitCode -ne 0) {
        $details = if (Test-Path -LiteralPath $logPath) {
            (Get-Content -LiteralPath $logPath -Tail 8) -join [Environment]::NewLine
        }
        else {
            'No launcher log was created.'
        }
        throw "The administrator launcher failed (exit code $($elevatedProcess.ExitCode)).`n`n$details"
    }

    Write-LauncherLog 'Launcher completed successfully.'
    Write-Host "Rocksmith TAB Overlay started. Launch log: $logPath"
}
catch {
    $message = $_.Exception.Message
    try { Write-LauncherLog "ERROR: $message" } catch { }
    $displayMessage = "$message`n`nDetails were saved to:`n$logPath"
    Show-LaunchError $displayMessage
    Write-Error $displayMessage
    exit 1
}
