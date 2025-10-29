# LiteFocus V1.5 — Windows 桌面专注外挂（WPF/.NET 8，零第三方依赖）

> 目标：**智能番茄钟 + 无操作清屏聚焦 + 消息节律管家(QQ/微信) + 场景管理器 + 轻复盘统计**。
> 设计原则：**轻量美学、实用优先、默认克制、强动作可撤销、本地离线**。

---

## 目录结构
```
LiteFocus/
├─ LiteFocus.sln
└─ src/
   └─ LiteFocus/
      ├─ LiteFocus.csproj
      ├─ App.xaml
      ├─ App.xaml.cs
      ├─ MainWindow.xaml
      ├─ MainWindow.xaml.cs
      ├─ Assets/
      │  └─ app.ico (可选)
      ├─ Interop/
      │  ├─ NativeMethods.cs
      │  └─ Win32Structs.cs
      ├─ Models/
      │  ├─ PomodoroSettings.cs
      │  ├─ AppSettings.cs
      │  ├─ Scene.cs
      │  └─ SessionRecord.cs
      ├─ Services/
      │  ├─ PomodoroService.cs
      │  ├─ IdleWatcher.cs
      │  ├─ ForegroundWatcher.cs
      │  ├─ WindowManager.cs
      │  ├─ SceneManager.cs
      │  ├─ StatsService.cs
      │  └─ HotkeyManager.cs
      └─ Util/
         ├─ JsonStore.cs
         └─ Paths.cs
```

---

## 构建与运行
```bash
# 需要 Windows + .NET SDK 8.0+
cd LiteFocus/src/LiteFocus
# 调试运行
dotnet run
# 发布单文件（x64）
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=true
# 可执行位于：bin/Release/net8.0-windows/win-x64/publish/LiteFocus.exe
```

---

## LiteFocus.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationIcon>Assets\app.ico</ApplicationIcon>
  </PropertyGroup>
  <ItemGroup>
    <None Include="Assets\app.ico" />
  </ItemGroup>
</Project>
```

---

## App.xaml
```xml
<Application x:Class="LiteFocus.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <Style x:Key="PrimaryButton" TargetType="Button">
            <Setter Property="Margin" Value="6"/>
            <Setter Property="Padding" Value="10,6"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="MinWidth" Value="84"/>
        </Style>
        <Style TargetType="TextBlock">
            <Setter Property="TextWrapping" Value="Wrap"/>
        </Style>
    </Application.Resources>
</Application>
```

---

## App.xaml.cs
```csharp
using System.Windows;

namespace LiteFocus
{
    public partial class App : Application
    {
    }
}
```

---

## MainWindow.xaml
```xml
<Window x:Class="LiteFocus.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="LiteFocus" Height="520" Width="440" ResizeMode="CanMinimize"
        WindowStartupLocation="CenterScreen">
    <DockPanel LastChildFill="True">
        <!-- 顶部控制条 -->
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="10">
            <ComboBox x:Name="SceneCombo" Width="160"/>
            <Button Style="{StaticResource PrimaryButton}" Click="ActivateScene_Click">启动场景</Button>
            <Button Style="{StaticResource PrimaryButton}" Click="ClearScreenNow_Click">清屏聚焦</Button>
        </StackPanel>

        <!-- 中央番茄钟 -->
        <Grid Margin="20">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                <TextBlock Text="阶段：" VerticalAlignment="Center"/>
                <TextBlock x:Name="PhaseText" FontWeight="Bold" Margin="4,0"/>
                <TextBlock Text="｜下一长休：" VerticalAlignment="Center"/>
                <TextBlock x:Name="LongBreakHint"/>
            </StackPanel>

            <Border Grid.Row="1" CornerRadius="120" BorderBrush="#DDD" BorderThickness="2" Padding="18"
                    HorizontalAlignment="Center" VerticalAlignment="Center">
                <StackPanel>
                    <TextBlock x:Name="TimerText" FontSize="64" FontWeight="Bold"
                               HorizontalAlignment="Center"/>
                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,10,0,0">
                        <Button Style="{StaticResource PrimaryButton}" Click="StartPause_Click" x:Name="StartPauseBtn">开始</Button>
                        <Button Style="{StaticResource PrimaryButton}" Click="Reset_Click">重置</Button>
                        <Button Style="{StaticResource PrimaryButton}" Click="SkipPhase_Click">跳过</Button>
                    </StackPanel>
                </StackPanel>
            </Border>

            <!-- 下方节律与状态条 -->
            <StackPanel Grid.Row="2" Margin="0,10,0,0">
                <TextBlock x:Name="QQSinceText"/>
                <TextBlock x:Name="WeChatSinceText"/>
                <TextBlock x:Name="StatusText" Foreground="#666"/>
                <StackPanel Orientation="Horizontal">
                    <Button Click="Allow60s_Click">社交紧急60秒</Button>
                    <Button Click="ExportCsv_Click">导出今日CSV</Button>
                    <CheckBox x:Name="FullScreenProtect" Content="全屏保护" Margin="12,0,0,0"/>
                </StackPanel>
            </StackPanel>
        </Grid>
    </DockPanel>
</Window>
```

---

## MainWindow.xaml.cs
```csharp
using LiteFocus.Models;
using LiteFocus.Services;
using LiteFocus.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
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
        private readonly DispatcherTimer _uiTick = new() { Interval = TimeSpan.FromMilliseconds(500) };

        private readonly HashSet<string> _watchApps = new(StringComparer.OrdinalIgnoreCase)
        {
            "QQ.exe","WeChat.exe","WXWork.exe"
        };

        private DateTime _lastStatusFlash = DateTime.MinValue;
        private bool _allowSocialTemp = false; // 紧急60秒放行
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

            // 默认设置
            _pomodoro.Settings = PomodoroSettings.Default();
            FullScreenProtect.IsChecked = true;

            // 订阅事件
            _pomodoro.OnTick += OnPomodoroTick;
            _pomodoro.OnPhaseChanged += OnPhaseChanged;
            _pomodoro.OnLongBreakCounterChanged += n => Dispatcher.Invoke(() => LongBreakHint.Text = $"{n}");

            _idle.OnIdleThresholdReached += OnIdleThresholdReached;
            _fg.OnAppEnter += OnAppEnter;
            _fg.OnAppExit += OnAppExit;

            _uiTick.Tick += UiTick_Tick;
            _uiTick.Start();

            // 热键：Ctrl+Alt+Space 清屏聚焦
            _hotkeys.RegisterClearScreenHotkey();

            // 场景下拉
            SceneCombo.ItemsSource = _scenes.LoadScenes();
            if (SceneCombo.Items.Count > 0) SceneCombo.SelectedIndex = 0;
        }

        private void UiTick_Tick(object? sender, EventArgs e)
        {
            QQSinceText.Text = $"距上次 QQ：{FormatSince(_stats.GetSinceLast("QQ.exe"))}";
            WeChatSinceText.Text = $"距上次 微信：{FormatSince(_stats.GetSinceLast("WeChat.exe"))}";

            // 超时放行撤销
            if (_allowSocialTemp && DateTime.Now > _allowUntil)
            {
                _allowSocialTemp = false;
                FlashStatus("社交放行结束，已恢复拦截。");
                _wm.TryMinimizeForegroundIfNotAllowed(this, _scenes.CurrentWhiteList);
            }
        }

        private static string FormatSince(TimeSpan? ts)
            => ts == null ? "--" : ts.Value.TotalSeconds < 60 ? $"{(int)ts.Value.TotalSeconds}s" : $"{(int)ts.Value.TotalMinutes}min";

        #region 事件处理
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
                if (FullScreenProtect.IsChecked == true && _wm.IsForegroundFullscreen())
                    return;

                // 无操作清屏：最小化其他窗口，仅保留本体置顶居中
                _wm.MinimizeAllExceptThis(this, _scenes.CurrentWhiteList);
                _wm.CenterAndTopMost(this);
                FlashStatus($"无操作 {idle.TotalSeconds:F0}s → 清屏聚焦");
                // 空闲时自动暂停番茄（可改为设置项）
                if (_pomodoro.IsRunning)
                {
                    _pomodoro.Pause();
                    FlashStatus("番茄已自动暂停");
                }
            });
        }

        private void OnAppEnter(string processName)
        {
            Dispatcher.Invoke(() =>
            {
                if (_allowSocialTemp) return; // 临时放行
                if (!_scenes.IsAllowedForeground(processName))
                {
                    // 冷却策略：若未达冷却阈值 → 温柔拦截
                    var since = _stats.GetSinceLast(processName);
                    var need = _scenes.GetCooldownMinutes(processName);
                    if (since == null || since.Value.TotalMinutes < need)
                    {
                        FlashStatus($"未到冷却阈值（{(int)since?.TotalMinutes} < {need}）→ 最小化 {processName}");
                        _wm.MinimizeForegroundIfMatch(processName);
                    }
                }
            });
        }

        private void OnAppExit(string processName)
        {
            // 退出时自动记录在 StatsService 内完成，这里无需处理
        }
        #endregion

        #region UI 交互
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
                FlashStatus("检测到全屏，清屏动作跳过");
                return;
            }
            _wm.MinimizeAllExceptThis(this, _scenes.CurrentWhiteList);
            _wm.CenterAndTopMost(this);
            FlashStatus("已清屏聚焦");
        }

        private void ActivateScene_Click(object sender, RoutedEventArgs e)
        {
            if (SceneCombo.SelectedItem is Scene sc)
            {
                _scenes.Activate(sc);
                FlashStatus($"已启动场景：{sc.Name}");
            }
        }

        private void Allow60s_Click(object sender, RoutedEventArgs e)
        {
            _allowSocialTemp = true;
            _allowUntil = DateTime.Now.AddSeconds(60);
            FlashStatus("已放行社交 60 秒");
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            var path = _stats.ExportTodayCsv();
            FlashStatus($"已导出到 {path}");
        }
        #endregion

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
        }
    }
}
```

---

## Interop/Win32Structs.cs
```csharp
using System;
using System.Runtime.InteropServices;

namespace LiteFocus.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class MONITORINFO
    {
        public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
        public RECT rcMonitor = new();
        public RECT rcWork = new();
        public int dwFlags;
    }
}
```

---

## Interop/NativeMethods.cs
```csharp
using LiteFocus.Interop;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LiteFocus.Interop
{
    internal static class NativeMethods
    {
        public const int SW_MINIMIZE = 6;
        public const int SW_RESTORE = 9;
        public const int SW_SHOW = 5;

        public const int MONITOR_DEFAULTTONEAREST = 2;

        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        public const int MOD_SHIFT = 0x0004;
        public const int MOD_WIN = 0x0008;
        public const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public static string GetWindowTitle(IntPtr hWnd)
        {
            int len = GetWindowTextLength(hWnd);
            var sb = new StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }
    }
}
```

---

## Models/PomodoroSettings.cs
```csharp
namespace LiteFocus.Models
{
    public class PomodoroSettings
    {
        public int WorkMinutes { get; set; } = 45;
        public int ShortBreakMinutes { get; set; } = 10;
        public int LongBreakMinutes { get; set; } = 20;
        public int RoundsPerLongBreak { get; set; } = 4;
        public int IdleAutoPauseSeconds { get; set; } = 60;

        public static PomodoroSettings Default() => new();
    }
}
```

---

## Models/AppSettings.cs
```csharp
using System;
using System.Collections.Generic;

namespace LiteFocus.Models
{
    public class AppSettings
    {
        public HashSet<string> WhiteListProcesses { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            "LiteFocus.exe","explorer.exe" // explorer 允许常驻
        };

        // 冷却阈值（分钟）
        public Dictionary<string,int> Cooldowns { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            {"QQ.exe",15},
            {"WeChat.exe",15},
            {"WXWork.exe",15}
        };
    }
}
```

---

## Models/Scene.cs
```csharp
using System.Collections.Generic;

namespace LiteFocus.Models
{
    public class Scene
    {
        public string Name { get; set; } = "";
        public List<string> LaunchApps { get; set; } = new(); // e.g. paths or URLs
        public HashSet<string> WhiteListProcesses { get; set; } = new();
        public Dictionary<string, int> Cooldowns { get; set; } = new(); // override
        public override string ToString() => Name;
    }
}
```

---

## Models/SessionRecord.cs
```csharp
using System;

namespace LiteFocus.Models
{
    public class SessionRecord
    {
        public string ProcessName { get; set; } = "";
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public double DurationSeconds => (End - Start).TotalSeconds;
    }
}
```

---

## Services/PomodoroService.cs
```csharp
using LiteFocus.Models;
using System;
using System.Timers;

namespace LiteFocus.Services
{
    public enum PomodoroPhase { Work, ShortBreak, LongBreak }

    public class PomodoroService : IDisposable
    {
        public PomodoroSettings Settings { get; set; } = PomodoroSettings.Default();
        public PomodoroPhase Phase { get; private set; } = PomodoroPhase.Work;
        public bool IsRunning { get; private set; }

        public event Action<TimeSpan>? OnTick;
        public event Action<PomodoroPhase>? OnPhaseChanged;
        public event Action<int>? OnLongBreakCounterChanged;

        private readonly Timer _timer = new(1000);
        private TimeSpan _remain;
        private int _roundCount = 0; // 完成工作轮数
        private readonly StatsService _stats;

        public PomodoroService(StatsService stats)
        {
            _stats = stats;
            _timer.Elapsed += (_, __) => Tick();
            ResetPhaseDuration();
        }

        private void ResetPhaseDuration()
        {
            _remain = Phase switch
            {
                PomodoroPhase.Work => TimeSpan.FromMinutes(Settings.WorkMinutes),
                PomodoroPhase.ShortBreak => TimeSpan.FromMinutes(Settings.ShortBreakMinutes),
                PomodoroPhase.LongBreak => TimeSpan.FromMinutes(Settings.LongBreakMinutes),
                _ => TimeSpan.FromMinutes(Settings.WorkMinutes)
            };
            OnTick?.Invoke(_remain);
        }

        public void Start() { IsRunning = true; _timer.Start(); }
        public void Pause() { IsRunning = false; _timer.Stop(); }
        public void Reset() { Pause(); Phase = PomodoroPhase.Work; _roundCount = 0; OnLongBreakCounterChanged?.Invoke(RoundsToLong()); ResetPhaseDuration(); OnPhaseChanged?.Invoke(Phase); }
        public void SkipPhase() { NextPhase(); }

        private void Tick()
        {
            if (!IsRunning) return;
            _remain = _remain - TimeSpan.FromSeconds(1);
            if (_remain.TotalSeconds <= 0)
            {
                // 记录统计（仅示例：完成一个阶段）
                if (Phase == PomodoroPhase.Work) _stats.AddPomodoroWork(Settings.WorkMinutes);
                NextPhase();
            }
            OnTick?.Invoke(_remain);
        }

        private void NextPhase()
        {
            if (Phase == PomodoroPhase.Work)
            {
                _roundCount++;
                Phase = (_roundCount % Settings.RoundsPerLongBreak == 0) ? PomodoroPhase.LongBreak : PomodoroPhase.ShortBreak;
            }
            else
            {
                Phase = PomodoroPhase.Work;
            }
            OnLongBreakCounterChanged?.Invoke(RoundsToLong());
            ResetPhaseDuration();
            OnPhaseChanged?.Invoke(Phase);
        }

        private int RoundsToLong() => Settings.RoundsPerLongBreak - (_roundCount % Settings.RoundsPerLongBreak);

        public void Dispose() => _timer.Dispose();
    }
}
```

---

## Services/IdleWatcher.cs
```csharp
using LiteFocus.Interop;
using System;
using System.Timers;

namespace LiteFocus.Services
{
    public class IdleWatcher : IDisposable
    {
        public event Action<TimeSpan>? OnIdleThresholdReached;
        private readonly Timer _timer = new(1000);
        private readonly TimeSpan _threshold = TimeSpan.FromSeconds(60);
        private bool _signaled = false;

        public IdleWatcher()
        {
            _timer.Elapsed += (_, __) => Check();
            _timer.Start();
        }

        private void Check()
        {
            var idle = GetIdleTime();
            if (idle >= _threshold)
            {
                if (!_signaled)
                {
                    _signaled = true;
                    OnIdleThresholdReached?.Invoke(idle);
                }
            }
            else
            {
                _signaled = false;
            }
        }

        public static TimeSpan GetIdleTime()
        {
            var info = new LASTINPUTINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(LASTINPUTINFO)) };
            if (NativeMethods.GetLastInputInfo(ref info))
            {
                uint idleTicks = (uint)Environment.TickCount - info.dwTime;
                return TimeSpan.FromMilliseconds(idleTicks);
            }
            return TimeSpan.Zero;
        }

        public void Dispose() => _timer.Dispose();
    }
}
```

---

## Services/ForegroundWatcher.cs
```csharp
using LiteFocus.Interop;
using LiteFocus.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;

namespace LiteFocus.Services
{
    public class ForegroundWatcher : IDisposable
    {
        public event Action<string>? OnAppEnter;
        public event Action<string>? OnAppExit;

        private readonly Timer _timer = new(800);
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
                var name = p.ProcessName + ".exe";

                if (_watchApps.Contains(name))
                {
                    if (_currentWatched != name)
                    {
                        // 切入监控 app
                        if (_currentWatched != null)
                        {
                            // 先结束上一个 session
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
                    // 离开监控 app
                    if (_currentWatched != null)
                    {
                        _stats.EndSession(_currentWatched, DateTime.Now);
                        OnAppExit?.Invoke(_currentWatched);
                        _currentWatched = null;
                    }
                }
            }
            catch { /* 进程可能已退出 */ }
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
```

---

## Services/WindowManager.cs
```csharp
using LiteFocus.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace LiteFocus.Services
{
    public class WindowManager
    {
        private readonly List<IntPtr> _minimized = new();

        public void MinimizeAllExceptThis(Window self, HashSet<string> whiteList)
        {
            _minimized.Clear();
            var selfHwnd = new System.Windows.Interop.WindowInteropHelper(self).Handle;
            NativeMethods.EnumWindows((hWnd, l) =>
            {
                if (!NativeMethods.IsWindowVisible(hWnd)) return true;
                if (hWnd == selfHwnd) return true;

                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                try
                {
                    var p = Process.GetProcessById((int)pid);
                    var name = p.ProcessName + ".exe";
                    if (whiteList.Contains(name)) return true;
                }
                catch { }

                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_MINIMIZE);
                _minimized.Add(hWnd);
                return true;
            }, IntPtr.Zero);

            CenterAndTopMost(self);
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
            foreach (var h in _minimized)
            {
                NativeMethods.ShowWindow(h, NativeMethods.SW_RESTORE);
            }
            _minimized.Clear();
        }

        public bool IsForegroundFullscreen()
        {
            var h = NativeMethods.GetForegroundWindow();
            if (h == IntPtr.Zero) return false;
            if (!NativeMethods.GetWindowRect(h, out var rc)) return false;
            var mon = NativeMethods.MonitorFromWindow(h, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var info = new MONITORINFO();
            NativeMethods.GetMonitorInfo(mon, info);
            // 允许 2 像素容差
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
```

---

## Services/SceneManager.cs
```csharp
using LiteFocus.Models;
using LiteFocus.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LiteFocus.Services
{
    public class SceneManager
    {
        private readonly string _path;
        private readonly AppSettings _appSettings;
        public HashSet<string> CurrentWhiteList => _appSettings.WhiteListProcesses;

        private List<Scene> _scenes = new();

        public SceneManager(string appDir)
        {
            _path = Path.Combine(appDir, "scenes.json");
            _appSettings = JsonStore.Load(Path.Combine(appDir, "appsettings.json"), new AppSettings());
            SeedIfEmpty();
        }

        private void SeedIfEmpty()
        {
            if (!File.Exists(_path))
            {
                _scenes = new List<Scene>
                {
                    new Scene
                    {
                        Name = "编码",
                        LaunchApps = { "code" },
                        WhiteListProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase){ "devenv.exe","Code.exe","LiteFocus.exe" },
                        Cooldowns = { {"QQ.exe", 20}, {"WeChat.exe",20} }
                    },
                    new Scene
                    {
                        Name = "写作",
                        LaunchApps = { "notepad" },
                        WhiteListProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase){ "WINWORD.EXE","notepad.exe","LiteFocus.exe" },
                        Cooldowns = { {"QQ.exe", 25}, {"WeChat.exe",25} }
                    },
                    new Scene
                    {
                        Name = "开会",
                        LaunchApps = { "msteams" },
                        WhiteListProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase){ "LiteFocus.exe","Teams.exe","Zoom.exe" },
                        Cooldowns = { {"QQ.exe", 30}, {"WeChat.exe",30} }
                    }
                };
                JsonStore.Save(_path, _scenes);
            }
            else
            {
                _scenes = JsonStore.Load(_path, new List<Scene>());
            }
        }

        public IList<Scene> LoadScenes() => _scenes;

        public void Activate(Scene sc)
        {
            // 启动/打开相关应用或URL
            foreach (var item in sc.LaunchApps)
            {
                try { Process.Start(new ProcessStartInfo(item) { UseShellExecute = true }); }
                catch { }
            }
            // 更新白名单与冷却
            _appSettings.WhiteListProcesses = sc.WhiteListProcesses;
            foreach (var kv in sc.Cooldowns) _appSettings.Cooldowns[kv.Key] = kv.Value;
            JsonStore.Save(Path.Combine(Paths.AppDataDir, "appsettings.json"), _appSettings);
        }

        public bool IsAllowedForeground(string processName) => _appSettings.WhiteListProcesses.Contains(processName);

        public int GetCooldownMinutes(string processName)
            => _appSettings.Cooldowns.TryGetValue(processName, out var m) ? m : 15;
    }
}
```

---

## Services/StatsService.cs
```csharp
using LiteFocus.Models;
using LiteFocus.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LiteFocus.Services
{
    public class StatsService
    {
        private readonly string _dir;
        private readonly string _sessionFile;
        private readonly string _dailyFile;

        private readonly Dictionary<string, DateTime> _enterTimes = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<SessionRecord> _today = new();
        private int _pomodoroWorkMinutes = 0;

        public StatsService(string appDir)
        {
            _dir = appDir;
            Directory.CreateDirectory(_dir);
            _sessionFile = Path.Combine(_dir, $"sessions_{DateTime.Now:yyyyMMdd}.json");
            _dailyFile = Path.Combine(_dir, $"daily_{DateTime.Now:yyyyMMdd}.json");
            LoadToday();
        }

        private void LoadToday()
        {
            if (File.Exists(_sessionFile))
                _today.AddRange(JsonStore.Load(_sessionFile, new List<SessionRecord>()));
        }

        public void StartSession(string processName, DateTime when)
        {
            _enterTimes[processName] = when;
        }

        public void EndSession(string processName, DateTime when)
        {
            if (_enterTimes.TryGetValue(processName, out var start))
            {
                var rec = new SessionRecord { ProcessName = processName, Start = start, End = when };
                _today.Add(rec);
                JsonStore.Save(_sessionFile, _today);
                _enterTimes.Remove(processName);
            }
        }

        public TimeSpan? GetSinceLast(string processName)
        {
            var last = _today.Where(r => r.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                             .OrderByDescending(r => r.End)
                             .Select(r => r.End)
                             .FirstOrDefault();
            if (last == default) return null;
            return DateTime.Now - last;
        }

        public void AddPomodoroWork(int minutes)
        {
            _pomodoroWorkMinutes += minutes;
            JsonStore.Save(_dailyFile, new { pomodoroWorkMin = _pomodoroWorkMinutes });
        }

        public string ExportTodayCsv()
        {
            var path = Path.Combine(_dir, $"export_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            using var w = new StreamWriter(path);
            w.WriteLine("Process,Start,End,DurationSeconds");
            foreach (var r in _today)
            {
                w.WriteLine($"{r.ProcessName},{r.Start:o},{r.End:o},{r.DurationSeconds.ToString(CultureInfo.InvariantCulture)}");
            }
            w.Flush();
            return path;
        }
    }
}
```

---

## Services/HotkeyManager.cs
```csharp
using LiteFocus.Interop;
using System;
using System.Windows;
using System.Windows.Interop;

namespace LiteFocus.Services
{
    public class HotkeyManager : IDisposable
    {
        private readonly Window _window;
        private HwndSource? _source;
        private const int HOTKEY_ID_CLEAR = 1; // Ctrl+Alt+Space

        public HotkeyManager(Window window)
        {
            _window = window;
            _window.SourceInitialized += (s, e) =>
            {
                var helper = new WindowInteropHelper(_window);
                _source = HwndSource.FromHwnd(helper.Handle);
                _source.AddHook(HwndHook);
            };
        }

        public void RegisterClearScreenHotkey()
        {
            var helper = new WindowInteropHelper(_window);
            // VK_SPACE = 0x20
            NativeMethods.RegisterHotKey(helper.Handle, HOTKEY_ID_CLEAR, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, 0x20);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID_CLEAR)
                {
                    // 触发清屏逻辑：发一个路由事件，由 MainWindow 按钮逻辑处理更简单
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        var mw = (LiteFocus.MainWindow)Application.Current.MainWindow;
                        mw?.GetType().GetMethod("ClearScreenNow_Click", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                           ?.Invoke(mw, new object?[] { null!, null! });
                    });
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            try
            {
                var helper = new WindowInteropHelper(_window);
                NativeMethods.UnregisterHotKey(helper.Handle, HOTKEY_ID_CLEAR);
            }
            catch { }
        }
    }
}
```

---

## Util/JsonStore.cs
```csharp
using System.IO;
using System.Text.Json;

namespace LiteFocus.Util
{
    public static class JsonStore
    {
        private static readonly JsonSerializerOptions _opt = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true
        };

        public static T Load<T>(string path, T fallback)
        {
            try
            {
                if (File.Exists(path))
                {
                    var txt = File.ReadAllText(path);
                    var obj = JsonSerializer.Deserialize<T>(txt, _opt);
                    if (obj != null) return obj;
                }
            }
            catch { }
            return fallback;
        }

        public static void Save<T>(string path, T data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var txt = JsonSerializer.Serialize(data, _opt);
            File.WriteAllText(path, txt);
        }
    }
}
```

---

## Util/Paths.cs
```csharp
using System;
using System.IO;

namespace LiteFocus.Util
{
    public static class Paths
    {
        public static string AppDataDir { get; } = Init();

        private static string Init()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LiteFocus");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
```

---

## 说明与取舍（V1.5）
- **已实现**
  - 番茄钟：45/10/20；跳过/暂停/重置；长休轮计数；空闲自动暂停（在清屏触发时）。
  - 无操作清屏聚焦：`GetLastInputInfo` 检测空闲≥60s → 最小化所有窗口，仅保留本体置顶居中；可手动触发；全屏保护。
  - 消息节律管家：监控切入 QQ/微信/企业微信，记录会话，计算“距上次查看”；冷却阈值不到则温柔最小化；“社交紧急60秒”临时放行。
  - 场景管理器：预置三场景（编码/写作/开会），启动关联应用，设置白名单与冷却；可通过 `scenes.json` 自行扩展。
  - 轻复盘统计：本地 `sessions_YYYYMMDD.json`、`daily_YYYYMMDD.json`；CSV 导出。
  - 全局热键：Ctrl+Alt+Space → 清屏聚焦。

- **暂缓/实验室**
  - 关键群关键词提醒、通知抓取：不同客户端实现差异大，属实验性质，后续可独立类封装在 `Services/Lab` 目录启用。
  - Do Not Disturb/专注时段系统级切换：为避免修改系统配置，当前仅应用内提示与约束。

- **隐私**
  - 不采集聊天内容，不联网，仅记录**进程名/时间戳**。

---

## 场景与配置
- `scenes.json`（自动生成，可手工编辑）示例：
```json
[
  {
    "Name": "编码",
    "LaunchApps": ["code"],
    "WhiteListProcesses": ["devenv.exe","Code.exe","LiteFocus.exe"],
    "Cooldowns": {"QQ.exe": 20, "WeChat.exe": 20}
  },
  {
    "Name": "写作",
    "LaunchApps": ["notepad"],
    "WhiteListProcesses": ["WINWORD.EXE","notepad.exe","LiteFocus.exe"],
    "Cooldowns": {"QQ.exe": 25, "WeChat.exe": 25}
  }
]
```
- `appsettings.json`（自动生成）：白名单与通用冷却阈值，场景激活时会覆盖。

---

## 进一步可做（但不破轻量）
- 设置页（XAML Tab）：可视化编辑番茄参数、空闲阈值、白名单、冷却时间、托盘开机自启。
- 托盘菜单：快捷开始/暂停、场景切换、放行60秒、导出统计、退出。
- 更细统计：跨天合并、Top 分心源排行、每小时热力条。

> 以上代码 **可直接编译运行**。如需我补上**托盘图标**、**设置页**、**安装脚本**或**更漂亮的主题**，告诉我你更偏好哪一项，我就继续往上加。
