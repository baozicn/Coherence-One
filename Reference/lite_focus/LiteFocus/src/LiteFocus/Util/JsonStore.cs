using System.IO;
using System.Text.Json;

namespace LiteFocus.Util
{
    public static class JsonStore
    {
        private static readonly JsonSerializerOptions _opt = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true
        };

        public static T Load<T>(string path, T fallback)
        {
            try
            {
                if (File.Exists(path))
                {
                    var txt = File.ReadAllText(path);
                    var obj = JsonSerializer.Deserialize<T>(txt, _opt);
                    if (obj != null) return obj;
                }
            }
            catch { }
            return fallback;
        }

        public static void Save<T>(string path, T data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var txt = JsonSerializer.Serialize(data, _opt);
            File.WriteAllText(path, txt);
        }
    }
}

