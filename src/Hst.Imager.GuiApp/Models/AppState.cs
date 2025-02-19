using ElectronNET.API;
using Hst.Imager.Core.Helpers;
using Hst.Imager.GuiApp.Helpers;
using System.Diagnostics;
using System.IO;
using System;

namespace Hst.Imager.GuiApp.Models
{
    public class AppState
    {
        public bool IsLicenseAgreed { get; set; }
        public bool IsAdministrator { get; set; }
        public bool IsElectronActive { get; set; }
        public bool IsWindows { get; set; }
        public bool IsMacOs { get; set; }
        public bool IsLinux { get; set; }
        public bool UseFake { get; set; }
        public string BaseUrl { get; set; }
        public string AppPath { get; set; }
        public string AppDataPath { get; set; }
        public string LogsPath { get; set; }
        public string ExecutingFile { get; set; }
        public Settings Settings { get; set; }

        public AppState()
        {
            Settings = new Settings();
        }

        public static AppState Create(string appDataPath, string baseUrl, bool isAdministrator) => new AppState
        {
            AppPath = AppContext.BaseDirectory,
            AppDataPath = appDataPath,
            LogsPath = Path.Combine(appDataPath, "logs"),
            ExecutingFile = WorkerHelper.GetExecutingFile(),
            BaseUrl = baseUrl,
            IsLicenseAgreed = ApplicationDataHelper.IsLicenseAgreed(appDataPath),
            IsAdministrator = isAdministrator,
            IsElectronActive = HybridSupport.IsElectronActive,
            UseFake = Debugger.IsAttached,
            IsWindows = Hst.Core.OperatingSystem.IsWindows(),
            IsMacOs = Hst.Core.OperatingSystem.IsMacOs(),
            IsLinux = Hst.Core.OperatingSystem.IsLinux()
        };
    }
}