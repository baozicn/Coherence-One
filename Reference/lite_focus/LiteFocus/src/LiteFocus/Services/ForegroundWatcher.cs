using LiteFocus.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Timers;

namespace LiteFocus.Services
{
    public class ForegroundWatcher : IDisposable
    {
        public event Action<string>? OnAppEnter;
        public event Action<string>? OnAppExit;

        private readonly System.Timers.Timer _timer = new(800);
        private readonly HashSet<string> _watchApps;
        private readonly StatsService _stats;
        private string? _currentWatched;
        private DateTime _enterTime;

        public ForegroundWatcher(HashSet<string> watchApps, StatsService stats)
        {
            _watchApps = watchApps;
            _stats = stats;
            _timer.Elapsed += (_, __) => Poll();
            _timer.Start();
        }

        private void Poll()
        {
            var h = NativeMethods.GetForegroundWindow();
            if (h == IntPtr.Zero) return;
            NativeMethods.GetWindowThreadProcessId(h, out uint pid);
            try
            {
                var p = Process.GetProcessById((int)pid);
                var raw = p.ProcessName + ".exe";
                var title = NativeMethods.GetWindowTitle(h);
                var name = Canonicalize(raw, title);

                if (_watchApps.Contains(raw) || _watchApps.Contains(name) || name is "QQ.exe" or "WeChat.exe" or "WXWork.exe")
                {
                    if (_currentWatched != name)
                    {
                        if (_currentWatched != null)
                        {
                            _stats.EndSession(_currentWatched, DateTime.Now);
                            OnAppExit?.Invoke(_currentWatched);
                        }
                        _currentWatched = name;
                        _enterTime = DateTime.Now;
                        _stats.StartSession(name, _enterTime);
                        OnAppEnter?.Invoke(name);
                    }
                }
                else
                {
                    if (_currentWatched != null)
                    {
                        _stats.EndSession(_currentWatched, DateTime.Now);
                        OnAppExit?.Invoke(_currentWatched);
                        _currentWatched = null;
                    }
                }
            }
            catch { }
        }

        private static string Canonicalize(string processName, string windowTitle)
        {
            // 统一 QQ / 微信 / 企业微信 的进程名，便于统计显示
            if (processName.Equals("TIM.exe", StringComparison.OrdinalIgnoreCase)) return "QQ.exe";
            if (processName.Equals("WeCom.exe", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("WeComDesktop.exe", StringComparison.OrdinalIgnoreCase)) return "WXWork.exe";
            if (processName.Equals("WeChatApp.exe", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("WeChatAppEx.exe", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("wechat.exe", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("Weixin.exe", StringComparison.OrdinalIgnoreCase)) return "WeChat.exe";

            // 根据窗口标题兜底
            if (!string.IsNullOrWhiteSpace(windowTitle))
            {
                var t = windowTitle.ToLowerInvariant();
                if (t.Contains("wechat") || t.Contains("微信")) return "WeChat.exe";
                if (t.Contains("wecom") || t.Contains("企业微信")) return "WXWork.exe";
                if (t.Contains("qq ") || t.Contains("qq-") || t.Equals("qq") || t.Contains("tim")) return "QQ.exe";
            }

            return processName;
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
            if (_currentWatched != null)
            {
                _stats.EndSession(_currentWatched, DateTime.Now);
            }
        }
    }
}
