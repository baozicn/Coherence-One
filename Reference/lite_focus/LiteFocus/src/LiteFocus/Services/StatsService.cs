using LiteFocus.Models;
using LiteFocus.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LiteFocus.Services
{
    public class StatsService
    {
        private readonly string _dir;
        private readonly string _sessionFile;
        private readonly string _dailyFile;

        private readonly Dictionary<string, DateTime> _enterTimes = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<SessionRecord> _today = new();
        private int _pomodoroWorkMinutes = 0;

        public StatsService(string appDir)
        {
            _dir = appDir;
            Directory.CreateDirectory(_dir);
            _sessionFile = Path.Combine(_dir, $"sessions_{DateTime.Now:yyyyMMdd}.json");
            _dailyFile = Path.Combine(_dir, $"daily_{DateTime.Now:yyyyMMdd}.json");
            LoadToday();
        }

        private void LoadToday()
        {
            if (File.Exists(_sessionFile))
                _today.AddRange(JsonStore.Load(_sessionFile, new List<SessionRecord>()));
        }

        public void StartSession(string processName, DateTime when)
        {
            _enterTimes[processName] = when;
        }

        public void EndSession(string processName, DateTime when)
        {
            if (_enterTimes.TryGetValue(processName, out var start))
            {
                var rec = new SessionRecord { ProcessName = processName, Start = start, End = when };
                _today.Add(rec);
                JsonStore.Save(_sessionFile, _today);
                _enterTimes.Remove(processName);
            }
        }

        public TimeSpan? GetSinceLast(string processName)
        {
            var last = _today.Where(r => r.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                             .OrderByDescending(r => r.End)
                             .Select(r => r.End)
                             .FirstOrDefault();
            if (last == default) return null;
            return DateTime.Now - last;
        }

        public void AddPomodoroWork(int minutes)
        {
            _pomodoroWorkMinutes += minutes;
            JsonStore.Save(_dailyFile, new { pomodoroWorkMin = _pomodoroWorkMinutes });
        }

        public string ExportTodayCsv()
        {
            var path = Path.Combine(_dir, $"export_{DateTime.Now:yyyyMMdd_HHmm}.csv");
            using var w = new StreamWriter(path);
            w.WriteLine("Process,Start,End,DurationSeconds");
            foreach (var r in _today)
            {
                w.WriteLine($"{r.ProcessName},{r.Start:o},{r.End:o},{r.DurationSeconds.ToString(CultureInfo.InvariantCulture)}");
            }
            w.Flush();
            return path;
        }
    }
}

