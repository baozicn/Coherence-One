import React, { useEffect, useMemo, useRef, useState } from "react";

// --- localStorage helper ---
function useLocalStorage(key, initialValue) {
  const [value, setValue] = useState(() => {
    try {
      const raw = localStorage.getItem(key);
      return raw ? JSON.parse(raw) : initialValue;
    } catch {
      return initialValue;
    }
  });
  useEffect(() => {
    try {
      localStorage.setItem(key, JSON.stringify(value));
    } catch {}
  }, [key, value]);
  return [value, setValue];
}

// --- 5-min countdown ---
function useCountdown(initialSeconds = 300) {
  const [seconds, setSeconds] = useState(initialSeconds);
  const [running, setRunning] = useState(false);
  const ref = useRef(null);
  useEffect(() => {
    if (!running) return;
    ref.current = setInterval(() => setSeconds((s) => (s > 0 ? s - 1 : 0)), 1000);
    return () => clearInterval(ref.current);
  }, [running]);
  const start = () => setRunning(true);
  const pause = () => setRunning(false);
  const reset = (n = initialSeconds) => {
    setRunning(false);
    setSeconds(n);
  };
  const mm = String(Math.floor(seconds / 60)).padStart(2, "0");
  const ss = String(seconds % 60).padStart(2, "0");
  return { seconds, display: `${mm}:${ss}`, running, start, pause, reset };
}

function Tip({ children }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white shadow-sm p-3 text-sm text-slate-700">
      {children}
    </div>
  );
}

function Section({ title, children, subtitle }) {
  return (
    <section className="mb-6">
      <div className="mb-2">
        <h2 className="text-lg font-semibold tracking-tight">{title}</h2>
        {subtitle && <p className="text-xs text-slate-500">{subtitle}</p>}
      </div>
      <div className="rounded-2xl bg-white/70 backdrop-blur border border-slate-200 shadow-sm p-4">
        {children}
      </div>
    </section>
  );
}

export default function App() {
  // Presets for general productivity
  const presetDefs = {
    study: {
      label: "学习",
      todayDefault: "打开资料只看一屏并做一条边注",
      actions: [
        "关闭 3 个无关窗口",
        "擦拭桌面 30 秒",
        "喝一口热饮",
        "写 3 行起点笔记（主题/目标/今日动作）",
        "看 5 分钟资料/视频",
        "检查手机静音",
        "换干净桌面背景",
      ],
    },
    writing: {
      label: "写作",
      todayDefault: "写一个 100 字草稿段落（不求好，只求有）",
      actions: [
        "关闭社交应用 15 分钟",
        "打开空白文档并写标题",
        "清空桌面杂物到一个盒子",
        "阅读 1 页参考",
        "写出 3 个小标题",
        "设定 5 分钟计时",
        "切换到专注模式",
      ],
    },
    coding: {
      label: "编码",
      todayDefault: "运行一次项目并看到输出/错误就记录",
      actions: [
        "关闭 3 个无关标签页",
        "打开终端并运行一次项目",
        "整理代码仓根目录 1 分钟",
        "写一行 TODO 注释",
        "阅读 README 的第一屏",
        "提交一次最小 commit（哪怕是注释）",
        "换干净桌面背景",
      ],
    },
    cleaning: {
      label: "打扫",
      todayDefault: "整理桌面左上角 5 分钟（计时）",
      actions: [
        "分类垃圾倒 1 袋",
        "清理 1 平方米表面",
        "折叠 5 件衣物",
        "清洗一个杯子",
        "擦拭键盘 30 秒",
        "整理电线 1 根",
        "通风 2 分钟",
      ],
    },
    fitness: {
      label: "健身",
      todayDefault: "计时 5 分钟核心/走楼梯/拉伸任一",
      actions: [
        "换上运动鞋",
        "喝 200 ml 水",
        "开窗热身 1 分钟",
        "播放热身音乐",
        "做 10 个深蹲",
        "拉伸 2 个动作",
        "准备毛巾",
      ],
    },
    review: {
      label: "复盘",
      todayDefault: "写下一条‘明天第一步’",
      actions: [
        "写 3 个今天做过的事",
        "标出 1 个最满意",
        "记录 1 个困扰",
        "给自己一句话",
        "清空收件箱 5 封",
        "同步待办 3 条",
        "整理一个截图",
      ],
    },
  };

  const [preset, setPreset] = useLocalStorage("mcs_eff_preset", "study");
  const actionsPool = useMemo(() => presetDefs[preset].actions, [preset]);

  // Minimal profile / drawer (generic)
  const [project, setProject] = useLocalStorage("mcs_eff_project", "我的主题/项目名（可留空）");
  const [context, setContext] = useLocalStorage("mcs_eff_context", "领域/场景（可留空）");
  const [smallGoal, setSmallGoal] = useLocalStorage("mcs_eff_smallGoal", "今天的小目标（示例：写 100 字、跑一次程序）");
  const [drawerOpen, setDrawerOpen] = useState(false);

  // Guided flow
  const [step, setStep] = useLocalStorage("mcs_eff_step", 0); // 0-3
  const [energy, setEnergy] = useLocalStorage("mcs_eff_energy", "mid");
  const [pickedAction, setPickedAction] = useLocalStorage("mcs_eff_action", actionsPool[0]);
  const [todayTask, setTodayTask] = useLocalStorage("mcs_eff_today", presetDefs[preset].todayDefault);
  const [doneAction, setDoneAction] = useLocalStorage("mcs_eff_doneAction", false);
  const [doneFocus, setDoneFocus] = useLocalStorage("mcs_eff_doneFocus", false);
  const [blocker, setBlocker] = useLocalStorage("mcs_eff_blocker", "");

  // update when preset changes
  useEffect(() => {
    setPickedAction(actionsPool[0]);
    setTodayTask(presetDefs[preset].todayDefault);
    // eslint-disable-next-line
  }, [preset]);

  const energyToDefault = (pool) => ({
    low: pool[2] || pool[0], // 更柔和：喝热饮类
    mid: pool[0], // 关闭 3 个无关窗口
    high: pool[3] || pool[0], // 写 3 行起点笔记类
  });

  useEffect(() => {
    if (step === 0) {
      const map = energyToDefault(actionsPool);
      setPickedAction(map[energy]);
    }
    // eslint-disable-next-line
  }, [energy, step, actionsPool]);

  const timer = useCountdown(300);

  const progress = useMemo(() => {
    const flags = [doneAction, doneFocus, !!blocker];
    const done = flags.filter(Boolean).length;
    return Math.round((done / 3) * 100);
  }, [doneAction, doneFocus, blocker]);

  const resetDay = () => {
    if (!confirm("重置今日引导进度？")) return;
    setStep(0);
    setDoneAction(false);
    setDoneFocus(false);
    setBlocker("");
    timer.reset(300);
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-slate-50 to-slate-100 text-slate-800">
      {/* Header */}
      <header className="mx-auto max-w-2xl px-4 pt-8 pb-4">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h1 className="text-xl font-semibold">最小自洽助理 · 效率启动器</h1>
            <p className="mt-1 text-sm text-slate-600">
              我们来从最小自洽因素一步步引导到今天的目标：
              <span className="font-medium"> 专注 × 轻负担 × 可完成</span>
            </p>
          </div>
          <button
            className="text-xs underline text-slate-500 hover:text-slate-700"
            onClick={resetDay}
          >重置今天</button>
        </div>

        {/* Preset & drawer */}
        <div className="mt-3 flex items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <span className="text-xs text-slate-600">场景</span>
            <div className="flex flex-wrap gap-2">
              {Object.entries(presetDefs).map(([k, v]) => (
                <button
                  key={k}
                  onClick={() => setPreset(k)}
                  className={`rounded-full px-3 py-1 text-xs border ${preset===k?"border-slate-900 bg-slate-900 text-white":"border-slate-300 bg-white hover:bg-slate-50"}`}
                >{v.label}</button>
              ))}
            </div>
          </div>
          <button
            onClick={() => setDrawerOpen((v) => !v)}
            className="text-xs rounded-full border border-slate-300 bg-white px-3 py-1 hover:bg-slate-50"
          >{drawerOpen ? "收起起点信息" : "展开起点信息"}</button>
        </div>
        {drawerOpen && (
          <div className="mt-3 grid grid-cols-1 md:grid-cols-2 gap-3 rounded-2xl border border-slate-200 bg-white p-3">
            <label className="flex flex-col gap-1 text-sm">
              <span className="text-slate-600">项目 / 主题</span>
              <input value={project} onChange={(e) => setProject(e.target.value)} className="rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-slate-300"/>
            </label>
            <label className="flex flex-col gap-1 text-sm">
              <span className="text-slate-600">领域 / 场景</span>
              <input value={context} onChange={(e) => setContext(e.target.value)} className="rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-slate-300"/>
            </label>
            <label className="md:col-span-2 flex flex-col gap-1 text-sm">
              <span className="text-slate-600">今天的小目标</span>
              <input value={smallGoal} onChange={(e) => setSmallGoal(e.target.value)} className="rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-slate-300"/>
            </label>
          </div>
        )}
      </header>

      {/* Main: stepper with progressive disclosure */}
      <main className="mx-auto max-w-2xl px-4 pb-16">
        {/* Stepper indicator */}
        <div className="mb-4 flex items-center gap-2">
          {[0,1,2,3].map((i) => (
            <div key={i} className={`h-2 flex-1 rounded-full ${i <= step ? "bg-slate-800" : "bg-slate-300"}`} />)
          )}
        </div>

        {step === 0 && (
          <Section title="准备进入状态" subtitle="选择能量 → 做一个最小动作">
            <div className="flex flex-wrap gap-2 mb-3">
              {[
                {k:"low", label:"低能量"},
                {k:"mid", label:"中等"},
                {k:"high", label:"有劲"},
              ].map((e) => (
                <button
                  key={e.k}
                  onClick={() => setEnergy(e.k)}
                  className={`rounded-xl border px-3 py-2 text-sm ${energy===e.k?"border-slate-900 bg-slate-900 text-white":"border-slate-300 bg-white hover:bg-slate-50"}`}
                >{e.label}</button>
              ))}
            </div>
            <Tip>
              建议动作（{presetDefs[preset].label}）：<span className="font-medium">{pickedAction}</span>
            </Tip>
            <div className="mt-3 flex items-center gap-2">
              <button
                onClick={() => setPickedAction(actionsPool[Math.floor(Math.random()*actionsPool.length)])}
                className="rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm hover:bg-slate-50"
              >换一个</button>
              <button
                onClick={() => { setDoneAction(true); setStep(1); }}
                className="rounded-xl bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800"
              >我做了</button>
            </div>
          </Section>
        )}

        {step === 1 && (
          <Section title="今天一个动作" subtitle="把门槛压到最低，用 5 分钟开机">
            <div className="flex flex-col gap-3">
              <input
                value={todayTask}
                onChange={(e) => setTodayTask(e.target.value)}
                className="rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-slate-300"
              />
              <Tip>只做这一件：设定 5 分钟计时 → 执行你填写的动作。完成或被打断都算“开机成功”。</Tip>
              <div className="flex items-center justify-between">
                <div className="text-3xl font-semibold tabular-nums">{timer.display}</div>
                <div className="flex items-center gap-2">
                  {!timer.running ? (
                    <button onClick={timer.start} className="rounded-xl bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800">开始 5 分钟</button>
                  ) : (
                    <button onClick={timer.pause} className="rounded-xl bg-slate-700 px-4 py-2 text-sm font-medium text-white hover:bg-slate-600">暂停</button>
                  )}
                  <button onClick={() => timer.reset(300)} className="rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm hover:bg-slate-50">重置</button>
                  <button onClick={() => { setDoneFocus(true); setStep(2); }} className="rounded-xl border border-slate-900 bg-white px-3 py-2 text-sm hover:bg-slate-50">我执行过一次</button>
                </div>
              </div>
            </div>
          </Section>
        )}

        {step === 2 && (
          <Section title="留下一条痕迹" subtitle="只记录 1 个卡点 / 下一步">
            <textarea
              value={blocker}
              onChange={(e) => setBlocker(e.target.value)}
              placeholder="例：卡在资料过多——明天只看目录；或：代码依赖安装失败——明天先解决依赖"
              className="w-full min-h-[120px] rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-slate-300"
            />
            <div className="mt-3 flex items-center justify-between">
              <div className="text-sm text-slate-600">今日总体进度</div>
              <div className="h-2 w-40 overflow-hidden rounded-full bg-slate-200">
                <div className="h-full bg-slate-800 transition-all" style={{ width: `${progress}%` }} />
              </div>
            </div>
            <div className="mt-3 flex items-center gap-2">
              <button onClick={() => setStep(1)} className="rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm hover:bg-slate-50">上一步</button>
              <button onClick={() => setStep(3)} className="rounded-xl bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800">完成今日回路</button>
            </div>
          </Section>
        )}

        {step === 3 && (
          <Section title="今日完成" subtitle="不需要完美，需要的是‘在推进’">
            <Tip>
              ✓ 最小动作：{doneAction ? "已完成" : "未完成"} · ✓ 执行一次：{doneFocus ? "已完成" : "未完成"} · ✓ 记录卡点：{blocker ? "已记录" : "未记录"}
            </Tip>
            <div className="mt-3 flex items-center gap-2">
              <button onClick={() => setStep(0)} className="rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm hover:bg-slate-50">再来一圈</button>
              <button onClick={resetDay} className="rounded-xl bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-800">清零明天再战</button>
            </div>
          </Section>
        )}

        {/* Optional resources (collapsed) */}
        <details className="mt-6">
          <summary className="cursor-pointer text-sm text-slate-600">需要资源时再展开</summary>
          <div className="mt-3 grid grid-cols-1 md:grid-cols-2 gap-2">
            <a href="#" className="rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm hover:bg-slate-50">模板/清单</a>
            <a href="#" className="rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm hover:bg-slate-50">教程/资料</a>
            <a href="#" className="rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm hover:bg-slate-50">专注工具指南</a>
            <a href="#" className="rounded-xl border border-slate-300 bg-white px-3 py-2 text-sm hover:bg-slate-50">备忘/收藏夹</a>
          </div>
        </details>

        <p className="mt-10 text-center text-xs text-slate-500">
          不用一下子变好。只要让今天多一点秩序气息。
        </p>
      </main>
    </div>
  );
}
