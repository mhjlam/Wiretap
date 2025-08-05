using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Wiretap.Models;

namespace Wiretap.Services
{
    public interface ISettingsService
    {
        Task<AppSettings> LoadSettingsAsync();
        Task SaveSettingsAsync(AppSettings settings);
        AppSettings GetDefaultSettings();
    }

    public class SettingsService : ISettingsService
    {
        private const string SettingsFileName = "settings.json";
        private readonly string _settingsPath;

        public SettingsService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "Wiretap");
            Directory.CreateDirectory(appFolder);
            _settingsPath = Path.Combine(appFolder, SettingsFileName);
        }

        public async Task<AppSettings> LoadSettingsAsync()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    return GetDefaultSettings();
                }

                var json = await File.ReadAllTextAsync(_settingsPath).ConfigureAwait(false);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                var result = settings ?? GetDefaultSettings();
                
                // Validate and fix any corrupted settings
                ValidateAndFixSettings(result);
                
                return result;
            }
            catch
            {
                return GetDefaultSettings();
            }
        }

        private void ValidateAndFixSettings(AppSettings settings)
        {
            // Ensure all nested objects exist
            settings.Window ??= new WindowSettings();
            settings.Listeners ??= new ListenerSettings();
            settings.Messages ??= new MessageSettings();
            
            // Validate window settings
            if (settings.Window.Width < 800) settings.Window.Width = 1280;
            if (settings.Window.Height < 600) settings.Window.Height = 720;
            if (settings.Window.MinWidth < 800) settings.Window.MinWidth = 1280;
            if (settings.Window.MinHeight < 600) settings.Window.MinHeight = 720;
            
            // Validate listener settings - ensure ports are within valid ranges
            if (settings.Listeners.DefaultUdpPort < 1 || settings.Listeners.DefaultUdpPort > 65535)
                settings.Listeners.DefaultUdpPort = 8080;
            if (settings.Listeners.DefaultTcpPort < 1 || settings.Listeners.DefaultTcpPort > 65535)
                settings.Listeners.DefaultTcpPort = 9090;
            
            // ShowConnectionStatus will use its loaded value or default to false - no forced override
            
            // Validate message settings
            if (settings.Messages.MaxMessages < 100) settings.Messages.MaxMessages = 1000;
            if (settings.Messages.FlashDurationMs < 50) settings.Messages.FlashDurationMs = 200;
            if (settings.Messages.FadeDurationMs < 50) settings.Messages.FadeDurationMs = 150;
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(_settingsPath, json).ConfigureAwait(false);
            }
            catch
            {
                // Silently handle save errors
            }
        }

        public AppSettings GetDefaultSettings()
        {
            return new AppSettings();
        }
    }
}