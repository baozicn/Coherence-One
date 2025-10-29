using System;
using System.Collections.Generic;

namespace LiteFocus.Models
{
    public class ProcessInfoEntry
    {
        public string ProcessName { get; set; } = "";
        public string? Path { get; set; }
    }

    public class ProcessSnapshot
    {
        public DateTime TakenAt { get; set; } = DateTime.Now;
        public string BootMarker { get; set; } = "";
        public List<ProcessInfoEntry> Running { get; set; } = new();
        public HashSet<string> VisibleProcessNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

