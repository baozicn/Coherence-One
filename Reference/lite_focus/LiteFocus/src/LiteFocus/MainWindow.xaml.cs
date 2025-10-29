using LiteFocus.Models;
using LiteFocus.Services;
using LiteFocus.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace LiteFocus
{
    public partial class MainWindow : Window
    {
        private readonly PomodoroService _pomodoro;
        private readonly IdleWatcher _idle;
        private readonly ForegroundWatcher _fg;
        private readonly WindowManager _wm;
        private readonly SceneManager _scenes;
        private readonly StatsService _stats;
        private readonly HotkeyManager _hotkeys;
        private readonly ProcessSnapshotService _procSnap;
        private readonly DispatcherTimer _uiTick = new() { Interval = TimeSpan.FromMilliseconds(500) };

        private readonly HashSet<string> _watchApps = new(StringComparer.OrdinalIgnoreCase)
        {
            // QQ 家族
            "QQ.exe","TIM.exe",
            // 微信家族
            "WeChat.exe","WeChatApp.exe","WeChatAppEx.exe","wechat.exe","Weixin.exe",
            // 企业微信/WeCom
            "WXWork.exe","WeCom.exe","WeComDesktop.exe"
        };

        private DateTime _lastStatusFlash = DateTime.MinValue;
        private bool _allowSocialTemp = false;
        private DateTime _allowUntil = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();

            var appDir = Paths.AppDataDir;
            _stats = new StatsService(appDir);
            _pomodoro = new PomodoroService(_stats);
            _idle = new IdleWatcher();
            _wm = new WindowManager();
            _scenes = new SceneManager(appDir);
            _fg = new ForegroundWatcher(_watchApps, _stats);
            _hotkeys = new HotkeyManager(this);
            _procSnap = new ProcessSnapshotService(appDir);

            _pomodoro.Settings = PomodoroSettings.Default();
            FullScreenProtect.IsChecked = true;
            AutoClearEnabled.IsChecked = true; // 启动默认自动清屏

            _pomodoro.OnTick += OnPomodoroTick;
            _pomodoro.OnPhaseChanged += OnPhaseChanged;
            _pomodoro.OnLongBreakCounterChanged += n => Dispatcher.Invoke(() => LongBreakHint.Text = $"还差{n}");

            _idle.OnIdleThresholdReached += OnIdleThresholdReached;
            _fg.OnAppEnter += OnAppEnter;
            _fg.OnAppExit += OnAppExit;

            _uiTick.Tick += UiTick_Tick;
            _uiTick.Start();

            _hotkeys.RegisterClearScreenHotkey();
            _procSnap.EnsureRunOnStartup();
            try { System.Windows.Application.Current.SessionEnding += Application_SessionEnding; } catch { }
            try { AppDomain.CurrentDomain.ProcessExit += (_, __) => { try { _procSnap.CaptureAndMergeSnapshot(); } catch { } }; } catch { }

            // 场景列表
            SceneCombo.ItemsSource = _scenes.LoadScenes();
            if (SceneCombo.Items.Count > 0) SceneCombo.SelectedIndex = 0;

            // 仅在“自启动且为本次开机首次运行”时恢复会话
            if (StartupManager.IsAutoStartLaunch() && _procSnap.IsFirstRunThisBoot())
            {
                FlashStatus("检测到开机首次启动，正在恢复上次会话...");
                _procSnap.TryRestoreLastSession(_wm, this);
            }
        }

        private void UiTick_Tick(object? sender, EventArgs e)
        {
            QQSinceText.Text = $"距上次QQ：{FormatSince(_stats.GetSinceLast("QQ.exe"))}";
            WeChatSinceText.Text = $"距上次微信：{FormatSince(_stats.GetSinceLast("WeChat.exe"))}";

            if (_allowSocialTemp && DateTime.Now > _allowUntil)
            {
                _allowSocialTemp = false;
                FlashStatus("社交临时放行结束");
            }
        }

        private static string FormatSince(TimeSpan? ts)
            => ts == null ? "--" : ts.Value.TotalSeconds < 60 ? $"{(int)ts.Value.TotalSeconds}秒" : $"{(int)ts.Value.TotalMinutes}分钟";

        private void OnPomodoroTick(TimeSpan remain)
        {
            Dispatcher.Invoke(() => TimerText.Text = remain.ToString(@"mm\:ss"));
        }

        private void OnPhaseChanged(PomodoroPhase phase)
        {
            Dispatcher.Invoke(() =>
            {
                PhaseText.Text = phase switch
                {
                    PomodoroPhase.Work => "工作",
                    PomodoroPhase.ShortBreak => "短休",
                    PomodoroPhase.LongBreak => "长休",
                    _ => ""
                };
                StartPauseBtn.Content = _pomodoro.IsRunning ? "暂停" : "开始";
            });
        }

        private void OnIdleThresholdReached(TimeSpan idle)
        {
            Dispatcher.Invoke(() =>
            {
                if (AutoClearEnabled.IsChecked != true)
                {
                    FlashStatus($"已关闭自动清屏（空闲 {idle.TotalSeconds:F0}s）");
                    return;
                }
                if (FullScreenProtect.IsChecked == true && _wm.IsForegroundFullscreen())
                    return;

                var passiveSet = FindVisiblePassiveMediaProcesses();
                var white = new HashSet<string>(_scenes.CurrentWhiteList, StringComparer.OrdinalIgnoreCase);
                foreach (var p in passiveSet) white.Add(p);

                bool preserveAny = passiveSet.Count > 0;
                _wm.MinimizeAllExceptThisSafe(this, white, centerSelf: !preserveAny);
                FlashStatus(preserveAny
                    ? $"无操作 {idle.TotalSeconds:F0}s → 保留媒体窗口（{string.Join(", ", passiveSet)}）并清理其它"
                    : $"无操作 {idle.TotalSeconds:F0}s → 清屏聚焦");
                if (_pomodoro.IsRunning)
                {
                    _pomodoro.Pause();
                    FlashStatus("检测到空闲，番茄已自动暂停");
                }
            });
        }

        private static readonly HashSet<string> _shellClasses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW"
        };

        private bool IsCriticalShellWindow(IntPtr hWnd)
        {
            var cls = LiteFocus.Interop.NativeMethods.GetWindowClassName(hWnd);
            return _shellClasses.Contains(cls);
        }

        private bool IsTopLevelAppWindow(IntPtr hWnd)
        {
            int style = LiteFocus.Interop.NativeMethods.GetWindowLong(hWnd, LiteFocus.Interop.NativeMethods.GWL_STYLE);
            int ex = LiteFocus.Interop.NativeMethods.GetWindowLong(hWnd, LiteFocus.Interop.NativeMethods.GWL_EXSTYLE);
            if ((style & LiteFocus.Interop.NativeMethods.WS_CHILD) == LiteFocus.Interop.NativeMethods.WS_CHILD) return false;
            if ((ex & LiteFocus.Interop.NativeMethods.WS_EX_TOOLWINDOW) == LiteFocus.Interop.NativeMethods.WS_EX_TOOLWINDOW) return false;
            if ((style & LiteFocus.Interop.NativeMethods.WS_DISABLED) == LiteFocus.Interop.NativeMethods.WS_DISABLED) return false;
            var owner = LiteFocus.Interop.NativeMethods.GetWindow(hWnd, LiteFocus.Interop.NativeMethods.GW_OWNER);
            if (owner != IntPtr.Zero) return false;
            return true;
        }

        private HashSet<string> FindVisiblePassiveMediaProcesses()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var selfHwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            LiteFocus.Interop.NativeMethods.EnumWindows((hWnd, l) =>
            {
                if (hWnd == selfHwnd) return true;
                if (!LiteFocus.Interop.NativeMethods.IsWindowVisible(hWnd)) return true;
                if (IsCriticalShellWindow(hWnd)) return true;
                if (!IsTopLevelAppWindow(hWnd)) return true;
                if (LiteFocus.Interop.NativeMethods.IsIconic(hWnd)) return true;

                LiteFocus.Interop.NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                try
                {
                    var p = Process.GetProcessById((int)pid);
                    var proc = p.ProcessName + ".exe";
                    var title = LiteFocus.Interop.NativeMethods.GetWindowTitle(hWnd);
                    if (_scenes.IsPassiveMediaProcess(proc) || _scenes.IsPassiveMediaTitle(title))
                    {
                        result.Add(proc);
                    }
                }
                catch { }
                return true;
            }, IntPtr.Zero);
            return result;
        }

        private void OnAppEnter(string processName)
        {
            Dispatcher.Invoke(() =>
            {
                var since = _stats.GetSinceLast(processName);
                if (since != null)
                {
                    FlashStatus($"你切到了 {processName}（距上次 {FormatSince(since)}）");
                }
            });
        }

        private void OnAppExit(string processName)
        {
            // no-op
        }

        private void StartPause_Click(object sender, RoutedEventArgs e)
        {
            if (_pomodoro.IsRunning) _pomodoro.Pause();
            else _pomodoro.Start();
            StartPauseBtn.Content = _pomodoro.IsRunning ? "暂停" : "开始";
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _pomodoro.Reset();
        }

        private void SkipPhase_Click(object sender, RoutedEventArgs e)
        {
            _pomodoro.SkipPhase();
        }

        private void ClearScreenNow_Click(object sender, RoutedEventArgs e)
        {
            if (FullScreenProtect.IsChecked == true && _wm.IsForegroundFullscreen())
            {
                FlashStatus("检测到全屏，已跳过清屏");
                return;
            }
            _wm.MinimizeAllExceptThisSafe(this, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            _wm.CenterAndTopMost(this);
            FlashStatus("已清屏聚焦");

            try
            {
                ClearedList.ItemsSource = null;
                ClearedList.ItemsSource = _wm.LastClearedSummaries;
            }
            catch { }
        }

        private void RestoreAll_Click(object sender, RoutedEventArgs e)
        {
            _wm.RestoreAll();
            FlashStatus("已恢复窗口");
            try { ClearedList.ItemsSource = null; } catch { }
        }

        private void ActivateScene_Click(object sender, RoutedEventArgs e)
        {
            if (SceneCombo.SelectedItem is Scene sc)
            {
                // 识别 VS Code 场景：名称为“VS Code工作区/编码/Coding/含code”，或启动项包含 code/Code.exe
                bool nameLooksCode =
                    (!string.IsNullOrEmpty(sc.Name) && (
                        sc.Name.IndexOf("VS Code工作区", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        sc.Name.IndexOf("编码", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        sc.Name.IndexOf("coding", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        sc.Name.IndexOf("code", StringComparison.OrdinalIgnoreCase) >= 0));
                bool isVsCodeScene = nameLooksCode || sc.LaunchApps.Any(a => a.IndexOf("code", StringComparison.OrdinalIgnoreCase) >= 0);
                if (isVsCodeScene)
                {
                    var folder = PickFolder();
                    if (!string.IsNullOrWhiteSpace(folder))
                    {
                        _scenes.ApplySceneSettings(sc);
                        if (OpenInVSCodeWorkspace(folder))
                            FlashStatus($"已将文件夹加入 VS Code 工作区：{folder}");
                        else
                            FlashStatus("打开 VS Code 失败，请确认已安装 code 命令");
                    }
                    else
                    {
                        FlashStatus("已取消选择文件夹");
                    }
                }
                else
                {
                    _scenes.Activate(sc);
                    FlashStatus($"已启动场景：{sc.Name}");
                }
            }
        }

        private static string? PickFolder()
        {
            try
            {
                using var dlg = new System.Windows.Forms.FolderBrowserDialog();
                dlg.Description = "选择 VS Code 工作区的文件夹";
                dlg.UseDescriptionForTitle = true;
                var res = dlg.ShowDialog();
                if (res == System.Windows.Forms.DialogResult.OK)
                {
                    return dlg.SelectedPath;
                }
            }
            catch { }
            return null;
        }

        private static bool OpenInVSCodeWorkspace(string folder)
        {
            try
            {
                // 生成临时 .code-workspace 文件并用新窗口打开，避免加入到已存在的窗口
                var ws = CreateWorkspaceFile(new[] { folder });
                if (StartCode($"--new-window \"{ws}\"")) return true;
            }
            catch { }
            return false;
        }

        private static string CreateWorkspaceFile(IEnumerable<string> folders)
        {
            var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LiteFocus", "vscode_workspaces");
            System.IO.Directory.CreateDirectory(dir);
            var name = $"ws_{DateTime.Now:yyyyMMdd_HHmmss_fff}.code-workspace";
            var file = System.IO.Path.Combine(dir, name);
            using (var sw = new System.IO.StreamWriter(file, false, System.Text.Encoding.UTF8))
            {
                sw.WriteLine("{");
                sw.WriteLine("  \"folders\": [");
                bool first = true;
                foreach (var f in folders)
                {
                    var pathEsc = f.Replace("\\", "/");
                    sw.WriteLine(first ? $"    {{ \"path\": \"{pathEsc}\" }}" : $",   {{ \"path\": \"{pathEsc}\" }}");
                    first = false;
                }
                sw.WriteLine("  ]");
                sw.WriteLine("}");
            }
            return file;
        }

        private static bool StartCode(string args)
        {
            try { Process.Start(new ProcessStartInfo("code", args) { UseShellExecute = true }); return true; } catch { }
            try
            {
                var local = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe");
                if (System.IO.File.Exists(local)) { Process.Start(new ProcessStartInfo(local, args) { UseShellExecute = true }); return true; }
                var programFiles = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft VS Code", "Code.exe");
                if (System.IO.File.Exists(programFiles)) { Process.Start(new ProcessStartInfo(programFiles, args) { UseShellExecute = true }); return true; }
            }
            catch { }
            return false;
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var path = _stats.ExportTodayCsv();
            FlashStatus($"已导出到 {path}");
        }

        private void Allow60s_Click(object sender, RoutedEventArgs e)
        {
            _allowSocialTemp = true;
            _allowUntil = DateTime.Now.AddSeconds(60);
            FlashStatus("已临时放行社交 60 秒");
        }

        private void FlashStatus(string msg)
        {
            _lastStatusFlash = DateTime.Now;
            StatusText.Text = $"[{_lastStatusFlash:HH:mm:ss}] {msg}";
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _hotkeys.Dispose();
            _idle.Dispose();
            _fg.Dispose();
            _pomodoro.Dispose();
            _procSnap.SaveSnapshotImmediate();
            _procSnap.Dispose();
            try { System.Windows.Application.Current.SessionEnding -= Application_SessionEnding; } catch { }
        }

        private void Application_SessionEnding(object? sender, SessionEndingCancelEventArgs e)
        {
            try { _procSnap.CaptureAndMergeSnapshot(); } catch { }
        }
    }
}
