using LiteFocus.Interop;
using LiteFocus.Models;
using LiteFocus.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows;

namespace LiteFocus.Services
{
    public class ProcessSnapshotService : IDisposable
    {
        private readonly string _snapshotPath;
        private readonly string _bootMarkerPath;
        private readonly System.Timers.Timer _timer;

        public ProcessSnapshotService(string appDir, int intervalSeconds = 120)
        {
            _snapshotPath = Path.Combine(appDir, "last_session_processes.json");
            _bootMarkerPath = Path.Combine(appDir, "last_boot_marker.txt");
            _timer = new System.Timers.Timer(Math.Max(30, intervalSeconds) * 1000);
            _timer.Elapsed += (_, __) => SaveSnapshotSafe();
            _timer.Start();
        }

        public void EnsureRunOnStartup() => StartupManager.EnsureRunOnStartup();

        public bool IsFirstRunThisBoot()
        {
            var cur = GetBootMarker();
            try
            {
                var last = File.Exists(_bootMarkerPath) ? File.ReadAllText(_bootMarkerPath) : "";
                bool first = !string.Equals(cur, last, StringComparison.OrdinalIgnoreCase);
                File.WriteAllText(_bootMarkerPath, cur);
                return first;
            }
            catch { return false; }
        }

        private static string GetBootMarker()
        {
            // 以系统启动时间作为标识
            try
            {
                var boot = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount64);
                return boot.ToString("O");
            }
            catch { return DateTime.Now.ToString("O"); }
        }

        public void SaveSnapshotSafe()
        {
            try { JsonStore.Save(_snapshotPath, CaptureSnapshot()); } catch { }
        }

        public void SaveSnapshotImmediate() => SaveSnapshotSafe();

        public void CaptureAndMergeSnapshot()
        {
            try
            {
                var cur = CaptureSnapshot();
                var prev = File.Exists(_snapshotPath) ? JsonStore.Load(_snapshotPath, new ProcessSnapshot()) : new ProcessSnapshot();
                var merged = new ProcessSnapshot
                {
                    TakenAt = cur.TakenAt,
                    BootMarker = cur.BootMarker,
                    Running = new List<ProcessInfoEntry>(),
                    VisibleProcessNames = new HashSet<string>(cur.VisibleProcessNames, StringComparer.OrdinalIgnoreCase)
                };

                // merge running by process name
                var map = new Dictionary<string, ProcessInfoEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in prev.Running) if (!map.ContainsKey(e.ProcessName)) map[e.ProcessName] = e;
                foreach (var e in cur.Running)
                {
                    if (map.TryGetValue(e.ProcessName, out var old))
                    {
                        // prefer path from current if available
                        if (!string.IsNullOrWhiteSpace(e.Path)) map[e.ProcessName] = e;
                    }
                    else map[e.ProcessName] = e;
                }
                merged.Running.AddRange(map.Values);

                // visible union
                foreach (var v in prev.VisibleProcessNames) merged.VisibleProcessNames.Add(v);

                JsonStore.Save(_snapshotPath, merged);
            }
            catch { }
        }

        public ProcessSnapshot CaptureSnapshot()
        {
            var snap = new ProcessSnapshot
            {
                TakenAt = DateTime.Now,
                BootMarker = GetBootMarker(),
            };

            // 1) 可见顶层窗口 -> 可视进程集合
            var visible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            NativeMethods.EnumWindows((hWnd, l) =>
            {
                if (!NativeMethods.IsWindowVisible(hWnd)) return true;
                if (IsCriticalShellWindow(hWnd)) return true;
                if (!IsTopLevelAppWindow(hWnd)) return true;
                if (NativeMethods.IsIconic(hWnd)) return true;
                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                try
                {
                    var p = Process.GetProcessById((int)pid);
                    var name = p.ProcessName + ".exe";
                    visible.Add(name);
                }
                catch { }
                return true;
            }, IntPtr.Zero);
            snap.VisibleProcessNames = visible;

            // 2) 运行中的带窗口进程（任何顶层窗口） -> 路径+进程名
            var running = new Dictionary<string, ProcessInfoEntry>(StringComparer.OrdinalIgnoreCase);
            NativeMethods.EnumWindows((hWnd, l) =>
            {
                if (IsCriticalShellWindow(hWnd)) return true;
                if (!IsTopLevelAppWindow(hWnd)) return true;
                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                try
                {
                    var p = Process.GetProcessById((int)pid);
                    var name = p.ProcessName + ".exe";
                    string? path = null;
                    try { path = p.MainModule?.FileName; } catch { }
                    if (!running.ContainsKey(name))
                    {
                        running[name] = new ProcessInfoEntry { ProcessName = name, Path = path };
                    }
                }
                catch { }
                return true;
            }, IntPtr.Zero);
            snap.Running = running.Values.ToList();

            return snap;
        }

        public void TryRestoreLastSession(WindowManager wm, Window self)
        {
            try
            {
                if (!File.Exists(_snapshotPath)) return;
                var snap = JsonStore.Load(_snapshotPath, new ProcessSnapshot());
                // 启动上次运行的进程（若未在运行，且有可执行路径）
                var current = Process.GetProcesses().Select(p => SafeName(p)).Where(n => n != null).ToHashSet(StringComparer.OrdinalIgnoreCase)!;
                foreach (var e in snap.Running)
                {
                    if (string.IsNullOrWhiteSpace(e.Path) || !File.Exists(e.Path)) continue;
                    if (current.Contains(e.ProcessName)) continue; // 已有同名进程在运行，避免重复
                    try
                    {
                        Process.Start(new ProcessStartInfo(e.Path) { UseShellExecute = true });
                    }
                    catch { }
                }

                // 延时应用显示状态（等待应用窗口创建）
                var delay = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
                delay.Tick += (s, e) =>
                {
                    try
                    {
                        var white = new HashSet<string>(snap.VisibleProcessNames, StringComparer.OrdinalIgnoreCase);
                        wm.MinimizeAllExceptThisSafe(self, white, centerSelf: false);
                    }
                    catch { }
                    (s as System.Windows.Threading.DispatcherTimer)!.Stop();
                };
                delay.Start();
            }
            catch { }
        }

        private static bool IsTopLevelAppWindow(IntPtr hWnd)
        {
            int style = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_STYLE);
            int ex = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
            if ((style & NativeMethods.WS_CHILD) == NativeMethods.WS_CHILD) return false;
            if ((ex & NativeMethods.WS_EX_TOOLWINDOW) == NativeMethods.WS_EX_TOOLWINDOW) return false;
            if ((style & NativeMethods.WS_DISABLED) == NativeMethods.WS_DISABLED) return false;
            var owner = NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER);
            if (owner != IntPtr.Zero) return false;
            return true;
        }

        private static bool IsCriticalShellWindow(IntPtr hWnd)
        {
            var cls = NativeMethods.GetWindowClassName(hWnd);
            return cls is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW";
        }

        private static string? SafeName(Process p)
        {
            try { return p.ProcessName + ".exe"; } catch { return null; }
        }

        public void Dispose()
        {
            try { _timer.Stop(); _timer.Dispose(); } catch { }
        }
    }
}
