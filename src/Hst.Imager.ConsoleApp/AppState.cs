using System.IO;
using System.Linq;
using Hst.Imager.Core.Models;

namespace Hst.Imager.ConsoleApp
{
    using System;
    using System.Reflection;
    using System.Threading;
    using Serilog.Core;

    public class AppState
    {
        private static readonly Lazy<AppState> AppStateInstance = new(() => new AppState(),
            LazyThreadSafetyMode.ExecutionAndPublication);

        public readonly string AppPath;
        public readonly LoggingLevelSwitch LoggingLevelSwitch;
        public readonly Version Version;
        public readonly DateTime BuildDate;
        public Settings Settings { get; set; }

        public bool UseCache { get; set; }
        public int BlockSize { get; set; }
        
        private AppState()
        {
            LoggingLevelSwitch = new LoggingLevelSwitch();
            
            var assembly = Assembly.GetExecutingAssembly();
            var executingFile = Environment.GetCommandLineArgs().FirstOrDefault();
            
            AppPath = string.IsNullOrEmpty(executingFile)
                ? Directory.GetCurrentDirectory()
                : Path.GetDirectoryName(executingFile);
            Version = assembly.GetName().Version;
            BuildDate = GetBuildDate(assembly);
            Settings = new Settings();
            UseCache = false;
            BlockSize = 1024 * 1024;
        }

        public static AppState Instance => AppStateInstance.Value;
        
        private static DateTime GetBuildDate(Assembly assembly)
        {
            var attribute = assembly.GetCustomAttribute<BuildDateAttribute>();
            return attribute?.DateTime ?? default(DateTime);
        }
    }
}