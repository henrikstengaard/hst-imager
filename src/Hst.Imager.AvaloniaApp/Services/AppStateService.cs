using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Models;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;

namespace Hst.Imager.AvaloniaApp.Services;

public class AppStateService : IAppStateService
{
    private readonly AppStateModel _appState;

    public AppStateService(AppStateModel appState)
    {
        _appState = appState;
    }

    public async Task<AppStateModel> GetAppStateAsync()
    {
        _appState.Settings = await ApplicationDataHelper.ReadSettings<Settings>(
            _appState.AppDataPath, Constants.AppName) ?? SettingsService.CreateDefaultSettings();
        return _appState;
    }

    public async Task<Settings> GetSettingsAsync()
    {
        var settings = await ApplicationDataHelper.ReadSettings<Settings>(
            _appState.AppDataPath, Constants.AppName) ?? SettingsService.CreateDefaultSettings();
        _appState.Settings = settings;
        return settings;
    }

    public async Task SaveSettingsAsync(Settings settings)
    {
        _appState.Settings = settings;
        await ApplicationDataHelper.WriteSettings(_appState.AppDataPath, Constants.AppName, settings);
    }
}
