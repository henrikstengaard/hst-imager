using System.Threading.Tasks;
using Hst.Imager.Core.Models;

namespace Hst.Imager.AvaloniaApp.Services;

public interface ISettingsService
{
    Task<Settings> GetSettingsAsync();
    Task SaveSettingsAsync(Settings settings);
}
