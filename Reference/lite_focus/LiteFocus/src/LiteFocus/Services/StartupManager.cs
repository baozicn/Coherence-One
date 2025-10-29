using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace LiteFocus.Services
{
    public static class StartupManager
    {
        private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string AppName = "LiteFocus";

        public static void EnsureRunOnStartup()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                if (key == null) return;

                var existing = key.GetValue(AppName) as string;
                var exePath = GetCurrentExecutablePath();
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return;

                var target = $"\"{exePath}\" --autostart";
                // 如果现有值不包含 --autostart 或不是当前路径，则更新
                if (!string.Equals(existing, target, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue(AppName, target);
                }
            }
            catch { }
        }

        public static bool IsAutoStartLaunch()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                foreach (var a in args)
                {
                    if (string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }
            catch { return false; }
        }

        private static string? GetCurrentExecutablePath()
        {
            try
            {
                using var p = Process.GetCurrentProcess();
                var path = p.MainModule?.FileName;
                return path;
            }
            catch { return null; }
        }
    }
}
