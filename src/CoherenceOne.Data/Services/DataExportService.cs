using System.Text;
using System.Text.Json;
using CoherenceOne.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace CoherenceOne.Data.Services;

public class DataExportService
{
    private readonly AppDbContext _dbContext;

    public DataExportService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> ExportTodayMarkdownAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var timeline = await _dbContext.Timeline
            .Where(e => DateOnly.FromDateTime(e.Ts.ToLocalTime().Date) == today)
            .OrderBy(e => e.Ts)
            .ToListAsync(cancellationToken);
        var notes = await _dbContext.Journal
            .Where(n => n.Date == today)
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine($"# CoherenceOne {today:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine("## Timeline");
        foreach (var ev in timeline)
        {
            sb.AppendLine($"- {ev.Ts:HH:mm:ss} `{ev.Type}` {ev.PayloadJson}");
        }

        sb.AppendLine();
        sb.AppendLine("## Journal");
        foreach (var note in notes)
        {
            sb.AppendLine($"- {string.Join(',', note.Tags)}: {note.Text}");
        }

        return sb.ToString();
    }

    public async Task<string> ExportTodayJsonAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var timeline = await _dbContext.Timeline
            .Where(e => DateOnly.FromDateTime(e.Ts.ToLocalTime().Date) == today)
            .OrderBy(e => e.Ts)
            .ToListAsync(cancellationToken);
        var notes = await _dbContext.Journal
            .Where(n => n.Date == today)
            .ToListAsync(cancellationToken);

        var payload = new
        {
            date = today,
            timeline,
            journal = notes
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
