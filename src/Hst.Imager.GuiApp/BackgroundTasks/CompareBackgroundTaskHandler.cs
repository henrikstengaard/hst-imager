using System.Linq;
using Hst.Imager.Core.Helpers;

namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System;
    using System.Threading.Tasks;
    using Core;
    using Core.Commands;
    using Core.Models;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Microsoft.Extensions.Logging;
    using Models;

    public class CompareBackgroundTaskHandler(
        ILoggerFactory loggerFactory,
        IPhysicalDriveManager physicalDriveManager,
        AppState appState)
        : IBackgroundTaskHandler
    {
        private readonly ILogger<CompareBackgroundTaskHandler> logger = loggerFactory.CreateLogger<CompareBackgroundTaskHandler>();

        public event EventHandler<ProgressEventArgs> ProgressUpdated;

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not CompareBackgroundTask compareBackgroundTask)
            {
                return;
            }

            try
            {
                // read settings enabling background worker to get changed settings from gui
                var settings = await ApplicationDataHelper.ReadSettings<Settings>(appState.AppDataPath, 
                    Core.Models.Constants.AppName) ?? new Settings();
                var physicalDrives = (await physicalDriveManager.GetPhysicalDrives(settings.AllPhysicalDrives))
                    .ToList();

                using var commandHelper = new CommandHelper(loggerFactory.CreateLogger<ICommandHelper>(), appState.IsAdministrator);
                var verifyCommand =
                    new CompareCommand(
                        loggerFactory.CreateLogger<CompareCommand>(),
                        commandHelper,
                        physicalDrives,
                        string.Concat(compareBackgroundTask.Byteswap ? "+bs:" : string.Empty, 
                            compareBackgroundTask.SourcePath),
                        compareBackgroundTask.SourceStartOffset,
                        compareBackgroundTask.DestinationPath,
                        compareBackgroundTask.DestinationStartOffset,
                        new Size(compareBackgroundTask.Size, Unit.Bytes), 
                        appState.Settings.Retries,
                        appState.Settings.Force,
                        appState.Settings.SkipUnusedSectors);
                verifyCommand.DataProcessed += (_, args) =>
                {
                    OnProgressUpdated(new Progress
                    {
                        Title = compareBackgroundTask.Title,
                        IsComplete = false,
                        PercentComplete = args.PercentComplete,
                        BytesPerSecond = args.BytesPerSecond,
                        BytesProcessed = args.BytesProcessed,
                        BytesRemaining = args.BytesRemaining,
                        BytesTotal = args.BytesTotal,
                        MillisecondsElapsed = args.PercentComplete > 0
                            ? (long)args.TimeElapsed.TotalMilliseconds
                            : null,
                        MillisecondsRemaining = args.PercentComplete > 0
                            ? (long)args.TimeRemaining.TotalMilliseconds
                            : null,
                        MillisecondsTotal = args.PercentComplete > 0
                            ? (long)args.TimeTotal.TotalMilliseconds
                            : null
                    });
                };

                var result = await verifyCommand.Execute(context.Token);

                OnProgressUpdated(new Progress
                {
                    Title = compareBackgroundTask.Title,
                    IsComplete = true,
                    HasError = result.IsFaulted,
                    ErrorMessage = result.IsFaulted ? result.Error.Message : null,
                    PercentComplete = 100
                });
            }
            catch (Exception e)
            {
                logger.LogError(e, "An unexpected error occured while executing compare command");

                OnProgressUpdated(new Progress
                {
                    Title = compareBackgroundTask.Title,
                    IsComplete = true,
                    HasError = true,
                    ErrorMessage = e.Message,
                    PercentComplete = 100
                });
            }
        }
        
        private void OnProgressUpdated(Progress progress)
        {
            ProgressUpdated?.Invoke(this, new ProgressEventArgs(progress));
        }
    }
}