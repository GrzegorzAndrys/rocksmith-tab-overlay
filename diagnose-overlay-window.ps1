$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class WindowProbe {
    public delegate bool Callback(IntPtr hwnd, IntPtr value);
    [DllImport("user32.dll")] public static extern bool EnumWindows(Callback callback, IntPtr value);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hwnd, ref Point point);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
    public struct Rect { public int Left, Top, Right, Bottom; }
    public struct Point { public int X, Y; }
}
'@

$rocksmith = Get-Process Rocksmith2014 -ErrorAction SilentlyContinue | Select-Object -First 1
$overlay = Get-Process RocksmithTabOverlay -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $rocksmith) { throw 'Rocksmith2014 is not running.' }
if (-not $overlay) { throw 'RocksmithTabOverlay is not running.' }
$foreground = [WindowProbe]::GetForegroundWindow()
[uint32]$foregroundPid = 0
[void][WindowProbe]::GetWindowThreadProcessId($foreground, [ref]$foregroundPid)
$windows = [Collections.Generic.List[object]]::new()
$callback = [WindowProbe+Callback]{
    param([IntPtr]$handle, [IntPtr]$value)
    [uint32]$windowPid = 0
    [void][WindowProbe]::GetWindowThreadProcessId($handle, [ref]$windowPid)
    if ($windowPid -eq $overlay.Id) {
        $rect = New-Object WindowProbe+Rect
        [void][WindowProbe]::GetWindowRect($handle, [ref]$rect)
        $windows.Add([pscustomobject]@{
            Handle = ('0x{0:X}' -f $handle.ToInt64())
            Visible = [WindowProbe]::IsWindowVisible($handle)
            Left = $rect.Left
            Top = $rect.Top
            Width = $rect.Right - $rect.Left
            Height = $rect.Bottom - $rect.Top
        })
    }
    return $true
}
[void][WindowProbe]::EnumWindows($callback, [IntPtr]::Zero)
$rocksmithClient = New-Object WindowProbe+Rect
$rocksmithOrigin = New-Object WindowProbe+Point
[void][WindowProbe]::GetClientRect($rocksmith.MainWindowHandle, [ref]$rocksmithClient)
[void][WindowProbe]::ClientToScreen($rocksmith.MainWindowHandle, [ref]$rocksmithOrigin)

[pscustomobject]@{
    ForegroundPid = $foregroundPid
    RocksmithPid = $rocksmith.Id
    RocksmithMainWindow = ('0x{0:X}' -f $rocksmith.MainWindowHandle.ToInt64())
    RocksmithClientLeft = $rocksmithOrigin.X
    RocksmithClientTop = $rocksmithOrigin.Y
    RocksmithClientWidth = $rocksmithClient.Right - $rocksmithClient.Left
    RocksmithClientHeight = $rocksmithClient.Bottom - $rocksmithClient.Top
    ScreenWidth = [WindowProbe]::GetSystemMetrics(0)
    ScreenHeight = [WindowProbe]::GetSystemMetrics(1)
    OverlayPid = $overlay.Id
    OverlayWindows = $windows
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'window-diagnostic.json') -Encoding UTF8
