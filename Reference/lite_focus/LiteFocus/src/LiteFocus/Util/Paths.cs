using System;
using System.IO;

namespace LiteFocus.Util
{
    public static class Paths
    {
        public static string AppDataDir { get; } = Init();

        private static string Init()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LiteFocus");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}

