using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Models;
using Hst.Imager.Core.Models;

namespace Hst.Imager.AvaloniaApp.Services;

public interface IAppStateService
{
    Task<AppStateModel> GetAppStateAsync();
    Task<Settings> GetSettingsAsync();
    Task SaveSettingsAsync(Settings settings);
}
