namespace CoherenceOne.Core.Models;

public record PomodoroDefaults(int Work, int Short, int Long);

public class Scene
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> LaunchApps { get; set; } = new();
    public List<string> Whitelist { get; set; } = new();
    public string? CooldownRules { get; set; }
    public PomodoroDefaults PomodoroDefaults { get; set; } = new(1500, 300, 900);
}
