namespace CoherenceOne.Core.Models;

public class TimelineEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Ts { get; set; } = DateTime.UtcNow;
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
}
