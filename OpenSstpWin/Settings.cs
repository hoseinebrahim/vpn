using System;
using System.IO;
using System.Text.Json;

namespace OpenSstpWin
{
    // The password is intentionally never saved.
    internal sealed class Settings
    {
        public string Server { get; set; } = "";
        public string Username { get; set; } = "";
        public bool UseDefaultGateway { get; set; } = true;

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenSstpWin", "settings.json");

        internal static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
                }
            }
            catch { }

            return new Settings();
        }

        internal void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
            }
            catch { }
        }
    }
}
