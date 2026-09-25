using Hst.Imager.Core.Models;

namespace Hst.Imager.AvaloniaApp.Models;

public class AppStateModel
{
    public bool IsAdministrator { get; set; }
    public bool IsWindows { get; set; }
    public bool IsMacOs { get; set; }
    public bool IsLinux { get; set; }
    public string AppDataPath { get; set; } = string.Empty;
    public string LogsPath { get; set; } = string.Empty;
    public Settings Settings { get; set; } = Services.SettingsService.CreateDefaultSettings();
}
