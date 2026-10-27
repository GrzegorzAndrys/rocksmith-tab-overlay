@echo off
setlocal
title Rocksmith TAB Overlay launcher

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-tab-overlay.ps1"
if errorlevel 1 (
    echo.
    echo The overlay could not start. The error is shown above and was saved to:
    echo %LOCALAPPDATA%\RocksmithTabOverlay\launcher.log
    echo.
    pause
)
