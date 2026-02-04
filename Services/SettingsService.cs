using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace Vague.Services
{
    public class SettingsService
    {
        private readonly string _settingsPath;

        public SettingsService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "Vague");
            Directory.CreateDirectory(appFolder);
            _settingsPath = Path.Combine(appFolder, "settings.json");
        }

        public void SaveSettings(VagueSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(_settingsPath, json);
            }
            catch
            {
            }
        }

        public VagueSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<VagueSettings>(json);
                    return settings ?? new VagueSettings();
                }
            }
            catch
            {
            }
            
            return new VagueSettings();
        }
    }

    public class VagueSettings
    {
        public List<SavedProcessInfo> PrivateProcesses { get; set; } = new();
        public bool MinimizeToTrayOnStartup { get; set; } = true;
    }

    public class SavedProcessInfo
    {
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public int BlurLevel { get; set; } = 95;
        public bool AutoUnblurOnFocus { get; set; } = true;
    }
}
