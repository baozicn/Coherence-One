using System.Diagnostics;
using System.Runtime.InteropServices;
using CoherenceOne.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoherenceOne.Services.System;

public sealed class SystemWatcher : ISystemWatcher, IDisposable
{
    private readonly ILogger<SystemWatcher>? _logger;
    private readonly HashSet<string> _passiveKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "会议", "课堂", "lecture", "meeting"
    };

    private readonly HashSet<string> _passiveProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "msteams", "teams", "zoom", "powerpnt", "msedge", "chrome"
    };

    public SystemWatcher(ILogger<SystemWatcher>? logger = null)
    {
        _logger = logger;
    }

    public bool IsIdle()
    {
        var lastInput = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref lastInput))
        {
            return false;
        }

        var idleTime = Environment.TickCount - (int)lastInput.dwTime;
        return idleTime > 30000;
    }

    public (string Process, string Title, bool IsFullScreen) Foreground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return (string.Empty, string.Empty, false);
        }

        var process = string.Empty;
        var title = GetWindowTitle(hwnd);
        uint pid;
        GetWindowThreadProcessId(hwnd, out pid);
        if (pid != 0)
        {
            try
            {
                process = Process.GetProcessById((int)pid).ProcessName;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Unable to read foreground process");
            }
        }

        var fullScreen = IsWindowFullScreen(hwnd);
        return (process, title, fullScreen);
    }

    public bool IsPassiveFocus()
    {
        if (IsIdle())
        {
            return false;
        }

        var (process, title, fullScreen) = Foreground();
        if (!fullScreen)
        {
            return false;
        }

        if (!_passiveProcesses.Contains(process))
        {
            return false;
        }

        return _passiveKeywords.Any(keyword => title.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsWindowFullScreen(IntPtr hwnd)
    {
        var rect = new RECT();
        if (!GetWindowRect(hwnd, ref rect))
        {
            return false;
        }

        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        MONITORINFO info = new() { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        return rect.Left <= info.rcMonitor.Left && rect.Top <= info.rcMonitor.Top && rect.Right >= info.rcMonitor.Right && rect.Bottom >= info.rcMonitor.Bottom;
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var buffer = new System.Text.StringBuilder(256);
        GetWindowText(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    public void Dispose()
    {
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, ref RECT rect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
}
