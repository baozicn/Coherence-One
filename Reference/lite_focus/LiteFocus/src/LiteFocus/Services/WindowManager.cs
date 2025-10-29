using LiteFocus.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

namespace LiteFocus.Services
{
    public class WindowManager
    {
        private readonly List<IntPtr> _minimized = new();
        private readonly List<string> _lastClearedSummaries = new();
        public IReadOnlyList<string> LastClearedSummaries => _lastClearedSummaries;
        private readonly List<IntPtr> _minimizedZTopToBottom = new();
        private IntPtr _lastTopMostBeforeClear = IntPtr.Zero;
        private static readonly HashSet<string> _shellClasses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Shell_TrayWnd",       // 任务栏
            "Shell_SecondaryTrayWnd", // 第二任务栏（多显示器）
            "Progman",             // 桌面管理器
            "WorkerW"              // 桌面图层
        };

        private static bool IsCriticalShellWindow(IntPtr hWnd)
        {
            var cls = NativeMethods.GetWindowClassName(hWnd);
            return _shellClasses.Contains(cls);
        }

        private static bool IsTopLevelAppWindow(IntPtr hWnd)
        {
            // 排除子窗口、工具窗口、禁用窗口；允许无最小化按钮但可被最小化的顶层窗口
            int style = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_STYLE);
            int ex = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
            if ((style & NativeMethods.WS_CHILD) == NativeMethods.WS_CHILD) return false;
            if ((ex & NativeMethods.WS_EX_TOOLWINDOW) == NativeMethods.WS_EX_TOOLWINDOW) return false;
            if ((style & NativeMethods.WS_DISABLED) == NativeMethods.WS_DISABLED) return false;
            // 如果有拥有者，一般认为是附属对话/浮层，跳过以避免残影
            var owner = NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER);
            if (owner != IntPtr.Zero) return false;
            return true;
        }

        public void MinimizeAllExceptThis(Window self, HashSet<string> whiteList, bool centerSelf = true)
        {
            _minimized.Clear();
            _lastClearedSummaries.Clear();
            _minimizedZTopToBottom.Clear();
            _lastTopMostBeforeClear = IntPtr.Zero;
            var selfHwnd = new System.Windows.Interop.WindowInteropHelper(self).Handle;
            NativeMethods.EnumWindows((hWnd, l) =>
            {
                if (!NativeMethods.IsWindowVisible(hWnd)) return true;
                if (hWnd == selfHwnd) return true;
                if (IsCriticalShellWindow(hWnd)) return true; // 跳过桌面/任务栏等系统窗口
                if (!IsTopLevelAppWindow(hWnd)) return true;  // 仅处理顶层应用窗口，跳过工具/浮层

                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                string procName = "(未知进程)";
                try
                {
                    var p = Process.GetProcessById((int)pid);
                    procName = p.ProcessName + ".exe";
                    if (whiteList.Contains(procName)) return true;
                }
                catch { }

                // 仅记录“原本未最小化”的窗口，避免恢复时误把本来最小化的窗口弹出
                bool wasMinimized = NativeMethods.IsIconic(hWnd);
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);
                if (!wasMinimized)
                {
                    _minimized.Add(hWnd);
                    _minimizedZTopToBottom.Add(hWnd);
                    if (_lastTopMostBeforeClear == IntPtr.Zero) _lastTopMostBeforeClear = hWnd;
                    var title = NativeMethods.GetWindowTitle(hWnd);
                    _lastClearedSummaries.Add($"{procName} — {title}");
                }
                return true;
            }, IntPtr.Zero);

            if (centerSelf)
                CenterAndTopMost(self);
            try
            {
                // 刷新桌面合成，清理可能残留的边框/影像
                NativeMethods.DwmFlush();
                var desktop = NativeMethods.GetDesktopWindow();
                NativeMethods.RedrawWindow(desktop, IntPtr.Zero, IntPtr.Zero,
                    NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_ALLCHILDREN | NativeMethods.RDW_ERASE | NativeMethods.RDW_UPDATENOW | NativeMethods.RDW_FRAME);
            }
            catch { }
        }

        // 改进版：仅当本次确有新窗口被最小化时，才覆盖上一轮“已清理窗口”记录，避免连续清屏导致记录被空覆盖。
        public void MinimizeAllExceptThisSafe(Window self, HashSet<string> whiteList, bool centerSelf = true)
        {
            var newMinimized = new List<IntPtr>();
            var newSummaries = new List<string>();
            var newZTopToBottom = new List<IntPtr>();
            IntPtr newTopMost = IntPtr.Zero;

            var selfHwnd = new System.Windows.Interop.WindowInteropHelper(self).Handle;
            NativeMethods.EnumWindows((hWnd, l) =>
            {
                if (!NativeMethods.IsWindowVisible(hWnd)) return true;
                if (hWnd == selfHwnd) return true;
                if (IsCriticalShellWindow(hWnd)) return true;
                if (!IsTopLevelAppWindow(hWnd)) return true;

                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                string procName = "(未知进程)";
                try
                {
                    var p = Process.GetProcessById((int)pid);
                    procName = p.ProcessName + ".exe";
                    if (whiteList.Contains(procName)) return true;
                }
                catch { }

                bool wasMinimized = NativeMethods.IsIconic(hWnd);
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);
                if (!wasMinimized)
                {
                    newMinimized.Add(hWnd);
                    newZTopToBottom.Add(hWnd);
                    if (newTopMost == IntPtr.Zero) newTopMost = hWnd;
                    var title = NativeMethods.GetWindowTitle(hWnd);
                    newSummaries.Add($"{procName} — {title}");
                }
                return true;
            }, IntPtr.Zero);

            if (centerSelf)
                CenterAndTopMost(self);
            try
            {
                NativeMethods.DwmFlush();
                var desktop = NativeMethods.GetDesktopWindow();
                NativeMethods.RedrawWindow(desktop, IntPtr.Zero, IntPtr.Zero,
                    NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_ALLCHILDREN | NativeMethods.RDW_ERASE | NativeMethods.RDW_UPDATENOW | NativeMethods.RDW_FRAME);
            }
            catch { }

            if (newMinimized.Count > 0)
            {
                _minimized.Clear();
                _lastClearedSummaries.Clear();
                _minimizedZTopToBottom.Clear();

                _minimized.AddRange(newMinimized);
                _lastClearedSummaries.AddRange(newSummaries);
                _minimizedZTopToBottom.AddRange(newZTopToBottom);
                _lastTopMostBeforeClear = newTopMost;
            }
        }

        public void CenterAndTopMost(Window w)
        {
            w.Topmost = true;
            var screenW = SystemParameters.PrimaryScreenWidth;
            var screenH = SystemParameters.PrimaryScreenHeight;
            w.Left = (screenW - w.Width) / 2;
            w.Top = (screenH - w.Height) / 2;
        }

        public void RestoreAll()
        {
            // 先按自底向上依次还原窗口
            for (int i = _minimizedZTopToBottom.Count - 1; i >= 0; i--)
            {
                var h = _minimizedZTopToBottom[i];
                try { NativeMethods.ShowWindow(h, NativeMethods.SW_RESTORE); } catch { }
            }

            // 再精确恢复 Z 序关系：从底到顶，逐个放到前一个之上
            IntPtr insertAfter = NativeMethods.HWND_BOTTOM;
            for (int i = _minimizedZTopToBottom.Count - 1; i >= 0; i--)
            {
                var h = _minimizedZTopToBottom[i];
                try
                {
                    NativeMethods.SetWindowPos(h, insertAfter, 0, 0, 0, 0,
                        NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER);
                    insertAfter = h;
                }
                catch { }
            }

            // 尝试将清屏前最上层窗口带回前台
            if (_lastTopMostBeforeClear != IntPtr.Zero)
            {
                try { NativeMethods.SetForegroundWindow(_lastTopMostBeforeClear); } catch { }
            }

            _minimized.Clear();
            _minimizedZTopToBottom.Clear();
            _lastClearedSummaries.Clear();
            _lastTopMostBeforeClear = IntPtr.Zero;
        }

        public bool IsForegroundFullscreen()
        {
            var h = NativeMethods.GetForegroundWindow();
            if (h == IntPtr.Zero) return false;
            if (!NativeMethods.GetWindowRect(h, out var rc)) return false;
            var mon = NativeMethods.MonitorFromWindow(h, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new MONITORINFO();
            NativeMethods.GetMonitorInfo(mon, info);
            bool nearFull = Math.Abs(rc.Left - info.rcMonitor.Left) <= 2 &&
                            Math.Abs(rc.Top - info.rcMonitor.Top) <= 2 &&
                            Math.Abs(rc.Right - info.rcMonitor.Right) <= 2 &&
                            Math.Abs(rc.Bottom - info.rcMonitor.Bottom) <= 2;
            return nearFull;
        }

        public void MinimizeForegroundIfMatch(string processName)
        {
            var h = NativeMethods.GetForegroundWindow();
            if (h == IntPtr.Zero) return;
            NativeMethods.GetWindowThreadProcessId(h, out uint pid);
            try
            {
                var p = Process.GetProcessById((int)pid);
                if ((p.ProcessName + ".exe").Equals(processName, StringComparison.OrdinalIgnoreCase))
                {
                    NativeMethods.ShowWindow(h, NativeMethods.SW_MINIMIZE);
                }
            }
            catch { }
        }

        public void TryMinimizeForegroundIfNotAllowed(Window self, HashSet<string> whiteList)
        {
            var h = NativeMethods.GetForegroundWindow();
            var selfHwnd = new System.Windows.Interop.WindowInteropHelper(self).Handle;
            if (h == IntPtr.Zero || h == selfHwnd) return;
            if (IsCriticalShellWindow(h)) return; // 不处理桌面/任务栏
            NativeMethods.GetWindowThreadProcessId(h, out uint pid);
            try
            {
                var p = Process.GetProcessById((int)pid);
                var name = p.ProcessName + ".exe";
                if (!whiteList.Contains(name)) NativeMethods.ShowWindow(h, NativeMethods.SW_MINIMIZE);
            }
            catch { }
        }
    }
}
