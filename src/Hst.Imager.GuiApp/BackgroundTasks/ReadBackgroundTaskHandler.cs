using System.Linq;
using Hst.Imager.Core.Helpers;

namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System;
    using System.Threading.Tasks;
    using Core;
    using Core.Commands;
    using Hst.Imager.Core.Models;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Microsoft.Extensions.Logging;
    using Models;

    public class ReadBackgroundTaskHandler(
        ILoggerFactory loggerFactory,
        IPhysicalDriveManager physicalDriveManager,
        AppState appState)
        : IBackgroundTaskHandler
    {
        private readonly ILogger<ReadBackgroundTaskHandler> logger = loggerFactory.CreateLogger<ReadBackgroundTaskHandler>();

        public event EventHandler<ProgressEventArgs> ProgressUpdated;

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not ReadBackgroundTask readBackgroundTask)
            {
                return;
            }

            try
            {
                OnProgressUpdated(new Progress
                {
                    Title = readBackgroundTask.Title,
                    IsComplete = false,
                    HasError = false,
                    ErrorMessage = null,
                    PercentComplete = 0
                });

                // read settings enabling background worker to get changed settings from gui
                var settings = await ApplicationDataHelper.ReadSettings<Settings>(appState.AppDataPath, 
                    Core.Models.Constants.AppName) ?? new Settings();
                var physicalDrives = (await physicalDriveManager.GetPhysicalDrives(settings.AllPhysicalDrives))
                    .ToList();

                using var commandHelper = new CommandHelper(loggerFactory.CreateLogger<ICommandHelper>(), appState.IsAdministrator);
                var readCommand =
                    new ReadCommand(loggerFactory.CreateLogger<ReadCommand>(), commandHelper, physicalDrives,
                        string.Concat(readBackgroundTask.Byteswap ? "+bs:" : string.Empty, 
                            readBackgroundTask.SourcePath),
                        readBackgroundTask.DestinationPath,
                        new Size(readBackgroundTask.Size, Unit.Bytes), appState.Settings.Retries,
                        appState.Settings.Verify, appState.Settings.Force, readBackgroundTask.StartOffset);
                readCommand.DataProcessed += (_, args) =>
                {
                    OnProgressUpdated(new Progress
                    {
                        Title = readBackgroundTask.Title,
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

                var result = await readCommand.Execute(context.Token);

                OnProgressUpdated(new Progress
                {
                    Title = readBackgroundTask.Title,
                    IsComplete = true,
                    HasError = result.IsFaulted,
                    ErrorMessage = result.IsFaulted ? result.Error.Message : null,
                    PercentComplete = 100
                });
            }
            catch (Exception e)
            {
                logger.LogError(e, "An unexpected error occured while executing read command");

                OnProgressUpdated(new Progress
                {
                    Title = readBackgroundTask.Title,
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