using System;
using System.Collections.Generic;
using System.Linq;
using CoherenceOne.Core.Interfaces;
using CoherenceOne.Services.Pomodoro;
using Xunit;

namespace CoherenceOne.Tests;

public class PomodoroServiceTests
{
    private class FakeStatsService : IStatsService
    {
        public List<string> Marks { get; } = new();

        public void Mark(string type, object? payload = null) => Marks.Add(type);

        public IEnumerable<Core.Models.TimelineEvent> Today() => Enumerable.Empty<Core.Models.TimelineEvent>();
    }

    [Fact]
    public void Start_ShouldRaiseTick()
    {
        var stats = new FakeStatsService();
        using var service = new PomodoroService(stats);
        (int left, string phase)? tick = null;
        service.Tick += (sender, e) => tick = e;

        service.Start(new PomodoroConfig(1500, 300, 900));

        Assert.NotNull(tick);
        Assert.Equal(1500, tick?.left);
        Assert.Equal("work", tick?.phase);
    }
}
