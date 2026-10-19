using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Models;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;

namespace Hst.Imager.AvaloniaApp.Services;

public class SettingsService : ISettingsService
{
    private readonly AppStateModel _appState;

    public SettingsService(AppStateModel appState)
    {
        _appState = appState;
    }

    /// <summary>
    /// Default settings for avalonia app. Use cache is disabled by default, as it slows down reading and writing
    /// physical disks and image files.
    /// </summary>
    public static Settings CreateDefaultSettings() => new() { UseCache = false };

    public async Task<Settings> GetSettingsAsync()
    {
        var settings = await ApplicationDataHelper.ReadSettings<Settings>(
            _appState.AppDataPath, Constants.AppName) ?? CreateDefaultSettings();
        _appState.Settings = settings;
        return settings;
    }

    public async Task SaveSettingsAsync(Settings settings)
    {
        _appState.Settings = settings;
        await ApplicationDataHelper.WriteSettings(_appState.AppDataPath, Constants.AppName, settings);
    }
}
