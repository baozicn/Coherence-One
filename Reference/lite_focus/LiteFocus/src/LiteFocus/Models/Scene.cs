using System.Collections.Generic;

namespace LiteFocus.Models
{
    public class Scene
    {
        public string Name { get; set; } = "";
        public List<string> LaunchApps { get; set; } = new();
        public HashSet<string> WhiteListProcesses { get; set; } = new();
        public Dictionary<string, int> Cooldowns { get; set; } = new();
        public override string ToString() => Name;
    }
}

