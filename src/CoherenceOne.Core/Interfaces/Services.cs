using CoherenceOne.Core.Models;

namespace CoherenceOne.Core.Interfaces;

public interface IFocusService
{
    void ClearScreen(bool respectPassiveFocus = true);
    void RestoreAll();
    void SetWhitelist(IEnumerable<string> processNames);
}

public record PomodoroConfig(int Work, int Short, int Long);

public interface IPomodoroService
{
    void Start(PomodoroConfig cfg);
    void Pause();
    void Skip();
    event EventHandler<(int leftSec, string phase)> Tick;
    event EventHandler<string> PhaseEnded;
}

public interface ISceneService
{
    Scene Load(string id);
    void Save(Scene scene);
    void Launch(string id);
}

public interface ISystemWatcher
{
    bool IsIdle();
    (string Process, string Title, bool IsFullScreen) Foreground();
    bool IsPassiveFocus();
}

public interface IStatsService
{
    void Mark(string type, object? payload = null);
    IEnumerable<TimelineEvent> Today();
}

public interface IJournalService
{
    void Add(JournalNote note);
    IEnumerable<JournalNote> List(DateOnly date);
}
