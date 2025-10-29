namespace CoherenceOne.Core.Models;

public class JournalNote
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string Text { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}
