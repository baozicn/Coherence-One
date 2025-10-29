namespace CoherenceOne.Core.Models;

public class Session
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int WorkMin { get; set; }
    public int PassiveMin { get; set; }
    public int Pomodoros { get; set; }
}
