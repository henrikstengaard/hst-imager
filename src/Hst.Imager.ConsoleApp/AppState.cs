﻿namespace Hst.Imager.ConsoleApp
{
    using System;
    using System.Reflection;
    using System.Threading;
    using Serilog.Core;

    public class AppState
    {
        private static readonly Lazy<AppState> AppStateInstance = new(() => new AppState(),
            LazyThreadSafetyMode.ExecutionAndPublication);

        public readonly LoggingLevelSwitch LoggingLevelSwitch;
        public readonly Version Version;
        public readonly DateTime BuildDate;

        private AppState()
        {
            LoggingLevelSwitch = new LoggingLevelSwitch();
            
            var assembly = Assembly.GetExecutingAssembly();
            Version = assembly.GetName().Version;
            BuildDate = GetBuildDate(assembly);
        }

        public static AppState Instance => AppStateInstance.Value;
        
        private static DateTime GetBuildDate(Assembly assembly)
        {
            var attribute = assembly.GetCustomAttribute<BuildDateAttribute>();
            return attribute?.DateTime ?? default(DateTime);
        }
    }
}