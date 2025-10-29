using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Linq;
using CoherenceOne.Core.Interfaces;
using CoherenceOne.Data;
using Microsoft.EntityFrameworkCore;

namespace CoherenceOne.Services.Focus;

public class FocusService : IFocusService
{
    private readonly AppDbContext _dbContext;
    private readonly ISystemWatcher _systemWatcher;
    private readonly ConcurrentStack<IntPtr> _minimizedWindows = new();
    private HashSet<string> _whitelist = new(StringComparer.OrdinalIgnoreCase);

    public FocusService(AppDbContext dbContext, ISystemWatcher systemWatcher)
    {
        _dbContext = dbContext;
        _systemWatcher = systemWatcher;
        LoadWhitelist();
    }

    public void ClearScreen(bool respectPassiveFocus = true)
    {
        if (respectPassiveFocus && _systemWatcher.IsPassiveFocus())
        {
            return;
        }

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
            {
                return true;
            }

            var processName = GetProcessName(hWnd);
            if (string.IsNullOrEmpty(processName))
            {
                return true;
            }

            if (_whitelist.Contains(processName))
            {
                return true;
            }

            _minimizedWindows.Push(hWnd);
            ShowWindow(hWnd, SW_FORCEMINIMIZE);
            return true;
        }, IntPtr.Zero);
    }

    public void RestoreAll()
    {
        while (_minimizedWindows.TryPop(out var handle))
        {
            ShowWindow(handle, SW_RESTORE);
        }
    }

    public void SetWhitelist(IEnumerable<string> processNames)
    {
        _whitelist = new HashSet<string>(processNames, StringComparer.OrdinalIgnoreCase);
        PersistWhitelist();
    }

    private void LoadWhitelist()
    {
        var scene = _dbContext.Scenes.AsNoTracking().FirstOrDefault();
        if (scene != null)
        {
            _whitelist = new HashSet<string>(scene.Whitelist, StringComparer.OrdinalIgnoreCase);
        }
    }

    private void PersistWhitelist()
    {
        var scene = _dbContext.Scenes.FirstOrDefault();
        if (scene == null)
        {
            return;
        }

        scene.Whitelist = _whitelist.ToList();
        _dbContext.SaveChanges();
    }

    private static string GetProcessName(IntPtr hwnd)
    {
        uint processId;
        GetWindowThreadProcessId(hwnd, out processId);
        if (processId == 0)
        {
            return string.Empty;
        }

        try
        {
            var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private const int SW_FORCEMINIMIZE = 11;
    private const int SW_RESTORE = 9;
}
