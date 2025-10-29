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

