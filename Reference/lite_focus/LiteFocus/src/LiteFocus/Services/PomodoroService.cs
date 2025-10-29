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

        private readonly System.Timers.Timer _timer = new(1000);
        private TimeSpan _remain;
        private int _roundCount = 0;
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
