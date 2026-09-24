using System.Reflection;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class AboutViewModel : ViewModelBase
{
    public string AppVersion { get; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
    public string AppName { get; } = "Hst Imager";
    public string Description { get; } = "Physical disk imager for Amiga computers.";
    public string Copyright { get; } = "Copyright © 2024 Henrik Nørfjand Stengaard";
}
