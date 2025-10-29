using System.Timers;
using CoherenceOne.Core.Interfaces;
using CoherenceOne.Core.Models;

namespace CoherenceOne.Services.Pomodoro;

public class PomodoroService : IPomodoroService, IDisposable
{
    private readonly IStatsService _statsService;
    private readonly Timer _timer;
    private PomodoroConfig _config = new(300, 300, 900);
    private string _phase = "work";
    private int _completedWorkSessions;
    private int _leftSeconds;
    private bool _running;

    public event EventHandler<(int leftSec, string phase)>? Tick;
    public event EventHandler<string>? PhaseEnded;

    public PomodoroService(IStatsService statsService)
    {
        _statsService = statsService;
        _timer = new Timer(1000);
        _timer.Elapsed += OnTimerElapsed;
    }

    public void Start(PomodoroConfig cfg)
    {
        _config = cfg;
        if (!_running)
        {
            _phase = "work";
            _leftSeconds = _config.Work;
            _timer.Start();
            _running = true;
        }
        Tick?.Invoke(this, (_leftSeconds, _phase));
    }

    public void Pause()
    {
        _timer.Stop();
        _running = false;
    }

    public void Skip()
    {
        AdvancePhase();
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_leftSeconds > 0)
        {
            _leftSeconds--;
            Tick?.Invoke(this, (_leftSeconds, _phase));
            if (_leftSeconds == 0)
            {
                PhaseEnded?.Invoke(this, _phase);
                OnPhaseCompleted();
            }
        }
    }

    private void OnPhaseCompleted()
    {
        if (_phase == "work")
        {
            _completedWorkSessions++;
            _statsService.Mark("pomodoro_work_end", new { duration = _config.Work });
        }

        AdvancePhase();
    }

    private void AdvancePhase()
    {
        if (_phase == "work")
        {
            _phase = (_completedWorkSessions % 4 == 0) ? "long" : "short";
            _leftSeconds = _phase == "long" ? _config.Long : _config.Short;
        }
        else
        {
            _phase = "work";
            _leftSeconds = _config.Work;
        }

        Tick?.Invoke(this, (_leftSeconds, _phase));
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
