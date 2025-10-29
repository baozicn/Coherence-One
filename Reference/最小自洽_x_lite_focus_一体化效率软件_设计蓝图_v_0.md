# 愿景（Vision）
打造一款**Windows 优先**、**美学友好**、**心智x环境一体化**的效率软件：把“最小自洽（Minimal Coherence System, MCS）”的轻引导与 LiteFocus 的系统级专注力治理整合为一套**从动机到现场**的闭环工具。

- **一句话**：先把心智“开机”，再把环境“打扫干净”，然后**轻负担地做一件小事**。
- **三大价值**：
  1) 低能量也能动（MCS 轻引导，5 分钟可达）；
  2) 桌面立即安静（清屏聚焦/白名单/冷却/番茄节律）；
  3) 留痕可复盘（会话快照、卡点&下一步、日/周回顾）。

---

# 设计原则（Design Tenets）
1. **最小可感完成**：任何操作链 ≤ 3 步，尽量 1-Click 达成。  
2. **温柔而坚定**：不羞辱用户的拖延，尊重“被动专注”（上课/看视频/听讲）为有效投入。  
3. **默认安静**：默认折叠“资源区/高级设置”，仅在需要时出现。  
4. **可单手使用**：核心交互支持键盘热键与触控友好按钮。  
5. **本地优先，温和同步**：先本地持久化；如需跨设备，仅做“文件级同步”（可选）。

---

# 功能整合地图（Capability Map）
| 领域 | 子功能 | 整合思路 |
|---|---|---|
| **MCS 轻引导** | 能量选择（低/中/高）、最小动作库、四步引导（选动作→5分钟→留痕→回路完成）、起点信息抽屉 | 作为**首页（Home）**与**微任务启动器**，任何场景都从此进入；完成后可直接触发“清屏聚焦＋番茄”。 |
| **番茄计时** | Work/Short/Long 相位、开始/暂停/跳过、长休计数 | 与“最小动作”联动：开始 5 分钟→可自动接力为工作番茄；UI 提供极简圆环计时。 |
| **环境治理** | 一键清屏聚焦、白名单、冷却、全屏保护、恢复窗口 | 作为“**专注键**（Ctrl+Alt+Space）”，对所有场景通用；MCS 启动后自动调用。 |
| **场景管理** | 场景（Scene）= LaunchApps + 白名单 + 冷却策略 + 默认计时配置 | 将“学习/写作/会议/编程”等预设场景内化；平衡“心智意图”与“系统策略”。 |
| **被动专注识别** | 识别视频会议/课堂/长篇阅读等状态，避免误打断 | 被动专注时**不清屏，不提醒**；但记入投入时间（Passive Focus Track）。 |
| **留痕与复盘** | 卡点/下一步、会话快照、当日记录、日/周回顾 | MCS 的“留痕”直接写入当日记录；Lite 端的快照与时间线合并显示。 |
| **资源区** | 模板、教程、清单、快捷启动 | 以“抽屉式侧栏”存在；可绑定场景/最小动作的特定资源。 |

---

# 体验流程（Golden Path）
1. 打开应用 → **选择能量**（低/中/高）→ 推荐 1 个**最小动作**（可改）；
2. 一键 **开始 5 分钟**（圆环计时启动）→ 同时触发**清屏聚焦**（白名单保留）；
3. 5 分钟结束 → 填写**卡点/下一步**（一句话即可）→ 选择“接力番茄/收工”；
4. 结束后**一键恢复窗口**，当日时间线自动更新。

---

# 架构方案（Windows 优先，前后端一体）
**Shell：.NET 8 WPF + WebView2（嵌入 React UI）**  
- 优势：
  - 继承 LiteFocus 的稳定系统 API（窗口枚举、最小化/恢复、全局热键、进程快照、空闲检测）。
  - 前端以 **React + Tailwind** 实现“美学在线”的 MCS UI；
  - 通过 **JS↔.NET Bridge**（`window.native`）实现 UI 与系统层通信。

**可演进方向（v0.3+）**：抽象出 Native Core（C# Class Library），未来可被 **Tauri/Electron** 等跨端外壳复用。

---

# 技术分层（Layers）
- **UI 层（React/Tailwind）**：MCS 首页、番茄圆环、场景页、资源抽屉、时间线/统计。  
- **桥接层（JSInterop）**：`window.native.*` 调用本地能力（清屏、热键、快照、Idle、Foreground）。  
- **Native Core（C#）**：WindowManager、PomodoroService、SceneManager、Idle & Foreground Watchers、StatsService。  
- **存储层（SQLite + EF Core / LiteDB 二选一）**：本地优先，提供轻量 JSON 导入/导出。

---

# Bridge API 草案（JS → Native）
```ts
// 环境治理
window.native.focus.clearScreen(options?: { respectPassiveFocus?: boolean })
window.native.focus.restoreAll()
window.native.focus.setWhitelist(processNames: string[])

// 番茄
window.native.pomodoro.start(cfg?: { work?: number; short?: number; long?: number })
window.native.pomodoro.pause()
window.native.pomodoro.skip()
window.native.pomodoro.onTick((leftSec:number, phase:"work"|"short"|"long")=>void)

// 场景
window.native.scene.launch(sceneId: string)
window.native.scene.save(scene: SceneConfig)

// 统计/留痕
window.native.journal.addNote({ text: string, tags?: string[] })
window.native.timeline.mark(event: { type: string; payload?: any })

// 设备状态
window.native.system.isIdle()
window.native.system.onPassiveFocus((state:{app:string,title:string})=>void)
```

---

# 数据模型（简化）
- `Scenes(id, name, launchApps[], whitelist[], cooldownRules, pomodoroDefaults)`  
- `Sessions(id, date, totalWorkMin, passiveMin, pomodoros)`  
- `Snapshots(id, sessionId, timestamp, foregroundApp, openWindows[])`  
- `Journal(id, date, text, tags[])`  
- `Timeline(id, date, eventType, payloadJson)`

---

# 视觉与品牌（保持“审美在线”）
**主题：Cream Capsule Tree（CCT）风格**（柔和奶油底 + 胶囊标题 + 干净描边）
- 背景：`#F7F2E7`；描边：`#324B5C`（2px）；标题胶囊：`#FFD9A8`；子卡：`#FFF1CF`；说明条：`#FDFCF8`
- 字体：Noto Sans SC；图标：Lucide；圆角：`rounded-2xl`；阴影：柔和小投影。  
- 动效：Framer Motion 微动（淡入/上浮 8px）、计时圆环**呼吸节律**（Work 微放大，Break 微收缩）。

**关键组件**
- **MCS Stepper 卡**（能量选择 + 最小动作 + 5 分钟按钮＋痕迹输入）  
- **番茄圆环卡**（相位/剩余时间/一键跳过）  
- **场景卡**（Logo + 一键启动 + 策略摘要）  
- **时间线**（Today 的事件与笔记）  
- **资源抽屉**（按场景/动作自动过滤）

---

# MVP → Alpha → Beta（增量清单）
**MVP**（一体化跑通）
- MCS 首页：能量→最小动作→5 分钟→留痕。  
- 清屏聚焦：白名单、恢复、热键（Ctrl+Alt+Space）。  
- 番茄：开始/暂停/跳过 + 长休计数。  
- 场景：内置 3 个（写作/学习/会议），含 LaunchApps & 白名单。  
- 本地数据库：Journal/Timeline/Sessions。

**Alpha**（更顺手）
- 被动专注自动识别（Teams/Zoom/浏览器标题关键词）。  
- “接力番茄”：5 分钟完成后，弹出“一键接力 25 分钟”。  
- 每日回顾：今日时间线汇总 + 打卡导出（PNG/Markdown）。

**Beta**（更完整）
- 冷却策略（对社交/视频类应用的窗口前台抑制）。  
- 会话快照（打开窗口集的保存/一键恢复）。  
- 资源抽屉：场景/动作绑定的模板与外链。  
- 轻量同步（可选）：导出 `*.coherence` 文件至云盘实现跨设备。

---

# 关键风险与对策
- **系统 API 稳定性**：不同 Windows 版本的窗口枚举行为差异 → 建立回退路径（仅最小化/恢复，不做强制置顶）。  
- **被动专注误判**：
  - 方案：多信号融合（前台进程、标题关键词、空闲时长、全屏状态），允许用户快速纠错（“这也是专注”按钮）。  
- **打断成本**：清屏/提醒可能干扰 → 默认温柔策略；被动专注时**不打断不提醒**。  
- **本地数据安全**：SQLite/LiteDB 加密可选；一键导出/备份。

---

# 命名与标识（备选）
- **Coherence One（自洽一）** / **秩序岛** / **轻舟·自洽** / **MCS Focus**  
- Logo 方向：胶囊形 + 圆环计时的叠形，暖光渐变。

---

# 项目结构（建议）
```
/CoherenceOne
  /app               # .NET 8 WPF Host（WebView2）
    /Native          # WindowManager, PomodoroService, SceneManager, Stats
    /Bridge          # JS <-> .NET 双向桥
    /Assets          # 图标/Logo/MSIX 资源
  /web               # React + Tailwind UI
    /components
    /pages
    /styles
    /lib             # 与 Bridge 的 SDK（ts 定义）
  /storage           # 数据层（EF Core 或 LiteDB）
  /scripts           # 打包/签名/发布
```

---

# 立即可做（Next 72h 建议）
1. **确认 MVP 清单**（如上）并固化路标；  
2. **搭壳**：WPF + WebView2 打通 `window.native` 的 3 个方法：`focus.clearScreen` / `pomodoro.start` / `journal.addNote`；  
3. **MCS 首页 UI**：完成能量选择、最小动作卡、5 分钟圆环；  
4. **场景预设**（3 个）：写作/学习/会议，填好 LaunchApps 与白名单；  
5. **Alpha 预埋**：前台监控/被动专注的接口留空实现；  
6. **视觉样式**：落地 CCT 主题 Tokens（色板/圆角/阴影/动效时序表）。

---

# 成果验收（Smoke Tests）
- [ ] 打开应用 10 秒内：选择能量 → 启动 5 分钟 → 桌面被清理（仅编辑器保留）。
- [ ] 结束后：可以写下 1 句“卡点/下一步”，出现在今日时间线。
- [ ] 按热键：可恢复所有被最小化窗口。
- [ ] 切到视频会议：不会自动清屏，也不会弹提醒；结束后时间线记录了“被动专注 20 min”。

---

> v0.1 侧重“路线与接口”。如果你同意该蓝图，我将补上：
> - React 首页（MCS Stepper + 圆环）组件清单与 props；
> - WPF Bridge 代码样例（`WebView.CoreWebView2.AddHostObjectToScript` 方案）；
> - SQLite 表结构与迁移脚本草案。



---

# 伪代码 / 开发思路 v0.1
> 目标：把 **MCS 轻引导 × LiteFocus 环境治理** 融为一体，从“心智开机→清屏专注→番茄节律→留痕复盘”的闭环出发，先跑通 MVP，再增量到 Alpha/Beta。

## 1) 系统总流水线（高层伪代码）
```pseudocode
APP.START()
  Shell = WPFHost(WebView2)
  Bridge = HostObjects([FocusService, PomodoroService, SceneService, StatsService, JournalService, SystemWatcher])
  UI.Load(HomePage)

UI.HomePage:onEnergySelected(level)
  action = MinimalActionLibrary.pick(level)
  UI.Stepper.show(action)

UI.Stepper:onStartFiveMinutes(action)
  Bridge.FocusService.clearScreen({ respectPassiveFocus: true })
  Bridge.PomodoroService.start({ work: 300, short: 300, long: 900 })  // 5min微行动
  Stats.timeline.mark({ type: "micro_start", payload:{ action } })

Bridge.PomodoroService:onTick(leftSec, phase)
  UI.Timer.update(leftSec, phase)

Bridge.PomodoroService:onPhaseEnd()
  if phase == "work" and currentWorkLen == 300:           // 微行动结束
    UI.Stepper.showNoteInput()                             // 卡点/下一步
    Journal.addNote(UI.note)
    ask = UI.modal("接力25min番茄？")
    if ask == YES:
      Bridge.PomodoroService.start({ work: 1500, short: 300, long: 900 })
      Stats.timeline.mark({ type: "pomodoro_continue" })
    else:
      Bridge.FocusService.restoreAll()
      Stats.timeline.mark({ type: "micro_end" })
```

## 2) State Machines（关键状态机）
### a) MCS Stepper（最小引导）
```pseudocode
states: IDLE -> PICKED -> RUNNING -> NOTE -> DONE
transitions:
  IDLE --(select energy)-> PICKED
  PICKED --(start 5min)-> RUNNING
  RUNNING --(5min ends)-> NOTE
  NOTE --(save note)-> DONE
  DONE --(start next)-> PICKED | --(exit)-> IDLE
```

### b) Pomodoro（相位机）
```pseudocode
phases: WORK <-> SHORT_BREAK <-> WORK ... every Nth WORK -> LONG_BREAK
signals: start(), pause(), skip(), tick(1s)
outputs: onTick(leftSec, phase), onPhaseEnd(phase)
```

### c) Passive Focus Detector（被动专注识别）
```pseudocode
observables: foregroundProcess, windowTitle, isFullScreen, idleSeconds
rules:
  if isFullScreen and foregroundProcess in [Teams, Zoom, Edge, Chrome] and
     windowTitle contains any ["会议","课堂","直播","Lecture","Meeting"] and idleSeconds < 30
     => state = PASSIVE_FOCUS (don’t clear, don’t disturb)
  else => state = NORMAL
```

## 3) Native Core（C#，伪代码接口）
### a) FocusService（清屏/恢复/白名单/冷却）
```csharp
class FocusService {
  List<WindowHandle> minimized;
  HashSet<string> whitelist;   // process names
  CooldownRules cooldown;

  void ClearScreen(Options opt) {
    if (SystemWatcher.IsPassiveFocus() && opt.respectPassiveFocus) return;
    minimized = [];
    foreach (var win in Win32.EnumTopLevelWindows()) {
      if (ShouldMinimize(win)) {
        Win32.ShowWindow(win, MINIMIZE);
        minimized.Add(win);
      }
    }
  }

  bool ShouldMinimize(WindowHandle w) {
    var p = w.ProcessName;
    if (whitelist.Contains(p)) return false;
    if (cooldown.IsInCooldown(p)) return true; // keep minimized
    return true;
  }

  void RestoreAll() {
    foreach (var w in minimized) Win32.ShowWindow(w, RESTORE);
    minimized.Clear();
  }
}
```

### b) PomodoroService（计时相位）
```csharp
class PomodoroService {
  enum Phase { Work, ShortBreak, LongBreak }
  Timer t; int leftSec; Phase phase; int workCount;
  void Start(Config c) { phase = Work; leftSec = c.work; t.Start(1s); }
  void Tick() {
    leftSec--; EmitTick(leftSec, phase);
    if (leftSec <= 0) OnPhaseEnd();
  }
  void OnPhaseEnd() {
    EmitPhaseEnd(phase);
    if (phase == Work) { workCount++; phase = (workCount % 4 == 0) ? LongBreak : ShortBreak; leftSec = pickBreak(); }
    else { phase = Work; leftSec = cfg.work; }
  }
}
```

### c) SceneService（场景启动/策略应用）
```csharp
class SceneService {
  Scene Load(id);
  void Launch(id) {
    var s = Load(id);
    foreach (var app in s.LaunchApps) Process.Start(app);
    FocusService.SetWhitelist(s.Whitelist);
    PomodoroService.ApplyDefaults(s.PomodoroDefaults);
    Stats.Timeline.Mark("scene_launch", s.Summary());
  }
}
```

### d) Watchers（Idle/Foreground）
```csharp
class SystemWatcher {
  bool IsIdle() => GetLastInputTime() > threshold;
  (string proc, string title) Foreground() => Win32.GetForegroundWindowInfo();
  bool IsPassiveFocus() { /* 合并 IsIdle=false, FullScreen=true, proc/title 命中 */ }
}
```

### e) Storage（SQLite/LiteDB）与 Journal/Stats
```csharp
record JournalNote { Guid Id; Date Date; string Text; string[] Tags; }
record TimelineEvent { Guid Id; DateTime Ts; string Type; string PayloadJson; }

class JournalService { void AddNote(JournalNote n); IEnumerable<JournalNote> List(Date d); }
class StatsService   { void Mark(TimelineEvent e); IEnumerable<TimelineEvent> Today(); }
```

## 4) Bridge（JS ↔ .NET）
```csharp
// 绑定到 WebView2
webView.CoreWebView2.AddHostObjectToScript("native", new {
  focus = new FocusServiceProxy(...),
  pomodoro = new PomodoroServiceProxy(...),
  scene = new SceneServiceProxy(...),
  journal = new JournalServiceProxy(...),
  stats = new StatsServiceProxy(...),
  system = new SystemWatcherProxy(...)
});
```

```ts
// TS 侧声明（简化）
declare global {
  interface Window { native: any }
}

// 用法示例
await window.native.focus.clearScreen({ respectPassiveFocus: true })
window.native.pomodoro.onTick((left: number, phase: 'work'|'short'|'long') => setTimer(left, phase))
```

## 5) Web UI（React）组件与状态（伪代码）
```typescript
// 状态容器（可用 Zustand/Redux，伪代码）
state = {
  energy: 'low'|'mid'|'high',
  action: MinimalAction | null,
  timer: { left: number, phase: 'work'|'short'|'long' },
  notes: Note[],
  today: TimelineEvent[]
}

<Home>
  <McsStepper onPickEnergy=... onStartFiveMinutes=... />
  <PomodoroRing left=state.timer.left phase=state.timer.phase />
  <SceneStrip scenes=predefined onLaunch=window.native.scene.launch />
  <Timeline today={state.today} />
  <ResourceDrawer filters={action.tags} />
</Home>

// 交互流
McsStepper.onStartFiveMinutes = async (action) => {
  await window.native.focus.clearScreen({ respectPassiveFocus: true })
  window.native.pomodoro.start({ work: 300, short: 300, long: 900 })
  addTimeline({ type:'micro_start', payload:{ action } })
}

window.native.pomodoro.onTick((left, phase) => setTimer({ left, phase }))
window.native.pomodoro.onPhaseEnd((phase) => {
  if (phase==='work' && previousWorkLen==300) openNoteInput()
})
```

## 6) 数据表（SQLite，伪 DDL）
```sql
TABLE Scenes(
  id TEXT PRIMARY KEY,
  name TEXT,
  launch_apps TEXT,        -- JSON
  whitelist TEXT,          -- JSON
  cooldown_rules TEXT,     -- JSON
  pomodoro_defaults TEXT   -- JSON
);

TABLE Journal(
  id TEXT PRIMARY KEY,
  date TEXT,
  text TEXT,
  tags TEXT                -- JSON
);

TABLE Timeline(
  id TEXT PRIMARY KEY,
  ts TEXT,
  type TEXT,
  payload TEXT             -- JSON
);

TABLE Sessions(
  id TEXT PRIMARY KEY,
  date TEXT,
  work_min INT,
  passive_min INT,
  pomodoros INT
);
```

## 7) MVP 开发分解（两周冲刺）
- **D1-D2** Shell：WPF + WebView2，`window.native` 暴露 3 个方法（clearScreen / start / addNote）。
- **D3-D4** React：MCS Stepper + 5min 圆环 + 番茄 ring（相位/剩余）。
- **D5** FocusService：窗口枚举/最小化/恢复 + 白名单。
- **D6** PomodoroService：Tick/PhaseEnd 事件，JS 侧监听。
- **D7** Journal/Timeline：SQLite + 简单列表查看（Today）。
- **D8** 场景预设：写作/学习/会议 + 启动应用 + 白名单存取。
- **D9** PassiveFocus 检测（基础规则） + respectPassiveFocus 开关。
- **D10** 稳定性/边界测试 + 视觉收尾（CCT Tokens）。

## 8) 测试脚本（Smoke & 回归）
```pseudocode
TEST-001: 10s 内完成能量选择→5min启动→桌面仅保留编辑器
TEST-002: 5min 结束后弹记笔记，写入 Today
TEST-003: 热键恢复全部窗口
TEST-004: 全屏会议中触发专注键，不清屏（尊重被动专注）
TEST-005: 场景“学习”启动，打开 Notion/VSCode，白名单生效
```

## 9) 风险点到 DoD（Definition of Done）
- Win32 窗口枚举在不同版本行为差异 → 提供黑/白名单双保险 + 恢复栈。
- PassiveFocus 误判 → UI 快捷纠错按钮“这也是专注”。
- 数据一致性 → 统一通过 Bridge 写 SQLite，UI 读只读快照。
- 视觉：落地 CCT 主题 Token（色板/圆角/阴影/动效节律）并通过组件库复用。
```



---

# 开发人员对接指令（一次性交付版 · 从开端到最终效果）
> 适用于 Windows 10/11。目标：按画布蓝图一次性交付**“最小自洽 × LiteFocus”一体化效率软件**的可安装版本（MSIX）与源码仓库，开箱可用、审美在线、功能齐备、通过全量验收脚本。

## 0. 最终可见效果（交付成品必须呈现的体验）
- 打开应用 10 秒内进入首页：**能量选择 → 推荐最小动作 → 一键开始 5 分钟**。
- 点击“开始 5 分钟”后：桌面被**清屏聚焦**（白名单应用保留），UI 上出现**圆环计时**与进度。
- 5 分钟结束：弹出**卡点/下一步**输入框；确认后可**接力 25 分钟番茄**或**恢复窗口**。
- 顶部可选择**场景（写作/学习/会议）**：自动启动场景关联应用、白名单生效、计时配置切换。
- **被动专注识别**：全屏视频会议/课堂时，不打断不清屏，但**时间线记录投入时长**。
- **时间线/今日视图**可回看事件（微行动/番茄/场景启动/被动专注）与**笔记**；数据**本地保存**，可导出 Markdown/JSON。
- 系统热键 `Ctrl+Alt+Space` 触发/恢复专注；托盘图标可快速切换场景与查看计时。

## 1. 仓库与工程结构（必须一致）
```
CoherenceOne/
  app/                     # .NET 8 WPF Host（WebView2）
    Native/               # FocusService, PomodoroService, SceneService, Watchers, Stats, Journal, Storage
    Bridge/               # JS <-> .NET 双向桥
    Assets/               # 图标、MSIX 资源
    App.xaml, MainWindow.xaml.cs, ...
  web/                     # React + Tailwind UI
    components/
    pages/
    styles/
    lib/bridge-sdk.ts     # TS 声明 + 调用封装
    index.html, main.tsx
  storage/                 # EF Core（SQLite）迁移脚本与上下文
  scripts/                 # 打包/签名/集成构建脚本
  README.md
```

## 2. 环境与工具（固定版本）
- **.NET SDK 8.0.x**（WPF, WindowsDesktop）
- **WebView2 Runtime**（最新稳定）
- **Node.js 20 LTS** + **pnpm 9**
- **SQLite 3**（随 EF Core 自动部署）

## 3. 统一命名 & 品牌（CCT 主题）
- 应用名：**Coherence One**（包名 `com.coherence.one`）。
- 主题 Token：背景 `#F7F2E7`、描边 `#324B5C`（2px）、标题胶囊 `#FFD9A8`、子卡 `#FFF1CF`、说明条 `#FDFCF8`；圆角 `2xl`，微阴影。
- 字体：Noto Sans SC；图标：Lucide。

## 4. 从零到可运行（一次性落地流程）
### 4.1 初始化工程
```bash
# 根目录
pnpm -v       # 9.x
node -v       # 20.x

# 初始化前端
cd web
pnpm i
pnpm build    # 产物输出到 web/dist

# 生成后端
cd ../app
# 正常 dotnet new wpf 已完成，确保引用 WebView2 与 SQLite 的 NuGet 包
```
- WPF Post-build 事件将 `web/dist` 拷贝到 `app/bin/Release/net8.0-windows/wwwroot`。
- WebView2 以 `EnsureCoreWebView2Async` 加载本地 `wwwroot/index.html`。

### 4.2 实现 Bridge（JS↔.NET）
- 在 `MainWindow.xaml.cs`：
  - 初始化服务（见 §5），实例注入到 `Bridge`。
  - `CoreWebView2.AddHostObjectToScript("native", HostBridgeInstance)`。
- TS 侧 `lib/bridge-sdk.ts` 暴露 Promise 化 API（见 §6）。

### 4.3 实现 Native Core 服务（一次到位）
实现以下服务并通过单元测试：
- **FocusService**：枚举顶级窗口 → 依据白名单/冷却规则执行最小化；保存“被最小化的窗口句柄栈”；`RestoreAll()` 恢复。
- **PomodoroService**：事件 `Tick(1s) / PhaseEnd`；工况：`Work/ShortBreak/LongBreak`，可 `start/pause/skip`。
- **SceneService**：读取/保存 `Scene`；`Launch(id)` 启动 `LaunchApps`、设置白名单、应用计时默认项。
- **SystemWatcher**：
  - `IsIdle()`：通过 `GetLastInputInfo`；阈值默认 60s。
  - `Foreground()`：前台进程与窗口标题；全屏检测。
  - **PassiveFocus**：规则（全屏 + 目标进程 + 关键词 + 非空闲）→ 供 FocusService 尊重。
- **StatsService / JournalService**：写入 `TimelineEvent / JournalNote`；查询当日列表。
- **Storage**（EF Core / SQLite）：表结构见 §7；提供迁移与加密可选（连接字符串密码）。

### 4.4 前端 UI（React + Tailwind）
- 首页 **MCS Stepper 卡**（能量选择→最小动作→开始 5 分钟→记卡点）。
- **番茄圆环**组件：工作/休息相位与剩余秒数；微动效呼吸。
- **场景条**：三个内置场景卡（写作/学习/会议），一键 `launch(sceneId)`。
- **时间线 Today**：按时间倒序展示事件；**资源抽屉**随选择的动作/场景过滤。

### 4.5 热键与托盘
- 注册全局热键 `Ctrl+Alt+Space`（冲突自动降级并提示）。
- 托盘菜单：开/关专注、场景切换、查看计时、退出。

### 4.6 安装包
- 使用 **MSIX** 打包（图标、显示名、协议 `coherenceone://` 可选）。
- 安装后**立即可运行**（若缺 WebView2 Runtime，引导安装）。

## 5. 服务接口（C# 侧）
```csharp
public interface IFocusService {
  void ClearScreen(bool respectPassiveFocus = true);
  void RestoreAll();
  void SetWhitelist(string[] processNames);
}

public interface IPomodoroService {
  void Start(PomodoroConfig cfg);  // work, short, long
  void Pause();
  void Skip();
  event EventHandler<TickEvent> OnTick;
  event EventHandler<PhaseEvent> OnPhaseEnd; // phase: work/short/long
}

public interface ISceneService {
  Scene Load(string id);
  void Save(Scene s);
  void Launch(string id);
}

public interface IJournalService { void Add(JournalNote note); IEnumerable<JournalNote> List(DateOnly d); }
public interface IStatsService   { void Mark(TimelineEvent e); IEnumerable<TimelineEvent> Today(); }

public interface ISystemWatcher {
  bool IsIdle();
  ForegroundInfo Foreground();
  bool IsPassiveFocus();
}
```

## 6. Bridge API（JS 侧）
```ts
window.native.focus.clearScreen({ respectPassiveFocus?: boolean })
window.native.focus.restoreAll()
window.native.focus.setWhitelist(processNames: string[])

window.native.pomodoro.start({ work?: number, short?: number, long?: number })
window.native.pomodoro.pause()
window.native.pomodoro.skip()
window.native.pomodoro.onTick((leftSec:number, phase:'work'|'short'|'long')=>void)
window.native.pomodoro.onPhaseEnd((phase:'work'|'short'|'long')=>void)

window.native.scene.launch(id: string)
window.native.scene.save(scene: SceneConfig)

window.native.journal.addNote({ text: string, tags?: string[] })
window.native.stats.timelineToday(): Promise<TimelineEvent[]>
window.native.system.isIdle(): Promise<boolean>
window.native.system.onPassiveFocus((state:{app:string,title:string})=>void)
```

## 7. 数据表（SQLite · EF Core Migration）
```sql
Scenes(id TEXT PK, name TEXT, launch_apps TEXT, whitelist TEXT, cooldown_rules TEXT, pomodoro_defaults TEXT)
Journal(id TEXT PK, date TEXT, text TEXT, tags TEXT)
Timeline(id TEXT PK, ts TEXT, type TEXT, payload TEXT)
Sessions(id TEXT PK, date TEXT, work_min INT, passive_min INT, pomodoros INT)
```

## 8. 初始数据（内置场景）
```json
[
  {"id":"writing","name":"写作","launch_apps":["notepad"],"whitelist":["notepad","CoherenceOne"],"pomodoro_defaults":{"work":1500,"short":300,"long":900}},
  {"id":"study","name":"学习","launch_apps":["notion"],"whitelist":["notion","CoherenceOne"],"pomodoro_defaults":{"work":1500,"short":300,"long":900}},
  {"id":"meeting","name":"会议","launch_apps":["msteams"],"whitelist":["msteams","CoherenceOne"],"pomodoro_defaults":{"work":1500,"short":300,"long":900}}
]
```
> 说明：如 `notion:` 协议不可用，退回浏览器 `https://www.notion.so/`。

## 9. UI 组件完成定义（Done Means Done）
- **MCS Stepper**：能量（3 档）→ 最小动作推荐（可编辑）→ **开始 5 分钟**→ **卡点输入**→ 完成。
- **PomodoroRing**：显示 `phase` 与 `leftSec`；按钮：开始/暂停/跳过；**结束事件**准确。
- **SceneStrip**：卡片含图标/名称/摘要；点击 `launch` 生效且写入时间线。
- **Timeline**：展示事件（micro_start/micro_end/pomodoro_continue/scene_launch/passive_focus）；可导出 `.md/.json`。
- **ResourceDrawer**：随动作/场景过滤显示；默认折叠。

## 10. 验收脚本（一次性全量通过）
```pseudocode
AC-01: 启动后 10s 内可开始 5 分钟微行动，桌面仅白名单应用保留
AC-02: 5 分钟结束后弹记笔记，保存至 Journal，并显示在 Today 时间线
AC-03: 选择场景“学习”→ 打开 Notion（或浏览器）→ 白名单切换成功
AC-04: 被动专注：全屏 Teams 会议中触发专注键，未清屏；结束后 Today 记录 passive_focus >= 1min
AC-05: 番茄接力：微行动→询问弹窗→接力 25 分钟，Tick/PhaseEnd 准确
AC-06: 热键 `Ctrl+Alt+Space` 清屏与恢复有效，冲突时有降级提示
AC-07: 导出：Today 导出 `.md` 与 `.json` 文件成功
AC-08: 颜色/圆角/阴影/动效符合 CCT 主题；无明显布局跳动
AC-09: 断电重启后，数据（Journal/Timeline/Scenes）可回看
AC-10: 安装包（MSIX）可在未装 SDK 的目标机上安装运行（有 WebView2 引导）
```

## 11. 非功能性约束
- **启动性能**：冷启动 ≤ 3s（UI 可见），10s 内可点击“开始 5 分钟”。
- **稳定性**：连续运行 8 小时无崩溃；内存占用稳定（< 400MB）。
- **隐私**：所有数据默认**本地**，无外网传输；可选数据库加密。
- **可访问性**：键盘可达（Tab 顺序合理）、高对比文本、聚焦边框可见。

## 12. 交付物清单（一次性交付）
1) **源码仓库**（完整可编译）  
2) **MSIX 安装包** + 图标/证书说明  
3) **使用说明**（README：安装、快捷键、场景配置、导出）  
4) **测试报告**（按 §10 验收逐条截图/录屏）  
5) **设计标注**（CCT 主题 Token 与组件截图）  
6) **版本号**：`1.0.0`（语义化版本）

## 13. 质量门槛（拒收条件）
- 任何一条验收脚本未通过；
- UI 未符合 CCT 主题或交互与画布描述不一致；
- 被动专注误判频繁且不可快速纠错；
- 无法在干净环境（仅装 WebView2 Runtime）安装与运行。

> 本指令为**一次性对接**：开发团队按本文自检合格后提交交付物，评审仅依据 §10 验收脚本与 §11 非功能约束。

