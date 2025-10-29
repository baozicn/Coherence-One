using System;

namespace LiteFocus.Models
{
    public class SessionRecord
    {
        public string ProcessName { get; set; } = "";
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public double DurationSeconds => (End - Start).TotalSeconds;
    }
}

