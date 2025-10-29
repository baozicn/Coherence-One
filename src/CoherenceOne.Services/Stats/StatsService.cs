using System.Text.Json;
using System.Linq;
using CoherenceOne.Core.Interfaces;
using CoherenceOne.Core.Models;
using CoherenceOne.Data;
using Microsoft.EntityFrameworkCore;

namespace CoherenceOne.Services.Stats;

public class StatsService : IStatsService
{
    private readonly AppDbContext _dbContext;

    public StatsService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Mark(string type, object? payload = null)
    {
        var timelineEvent = new TimelineEvent
        {
            Ts = DateTime.UtcNow,
            Type = type,
            PayloadJson = payload == null ? string.Empty : JsonSerializer.Serialize(payload)
        };

        _dbContext.Timeline.Add(timelineEvent);
        _dbContext.SaveChanges();
    }

    public IEnumerable<TimelineEvent> Today()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return _dbContext.Timeline
            .AsNoTracking()
            .Where(e => DateOnly.FromDateTime(e.Ts.ToLocalTime().Date) == today)
            .OrderBy(e => e.Ts)
            .ToList();
    }
}
