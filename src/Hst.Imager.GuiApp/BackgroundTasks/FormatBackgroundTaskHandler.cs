using Hst.Imager.Core;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.BackgroundTasks;
using Hst.Imager.Core.Models;
using Hst.Imager.GuiApp.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;
using System.Linq;
using Hst.Imager.Core.Helpers;

namespace Hst.Imager.GuiApp.BackgroundTasks
{
    public class FormatBackgroundTaskHandler(
        ILoggerFactory loggerFactory,
        IPhysicalDriveManager physicalDriveManager,
        AppState appState)
        : IBackgroundTaskHandler
    {
        private readonly ILogger<FormatBackgroundTaskHandler> logger = loggerFactory.CreateLogger<FormatBackgroundTaskHandler>();

        public event EventHandler<ProgressEventArgs> ProgressUpdated;

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not FormatBackgroundTask formatBackgroundTask)
            {
                return;
            }

            try
            {
                OnProgressUpdated(new Progress
                {
                    Title = formatBackgroundTask.Title,
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
                var formatCommand = new FormatCommand(loggerFactory.CreateLogger<FormatCommand>(), loggerFactory,
                    commandHelper, physicalDrives,
                    string.Concat(formatBackgroundTask.Byteswap ? "+bs:" : string.Empty, formatBackgroundTask.Path),
                    formatBackgroundTask.FormatType, formatBackgroundTask.FileSystem, 
                    formatBackgroundTask.FileSystemPath,
                    appState.AppDataPath,
                    new Size(formatBackgroundTask.Size, Unit.Bytes),
                    formatBackgroundTask.MaxPartitionSize);
                formatCommand.DataProcessed += (_, args) =>
                {
                    OnProgressUpdated(new Progress
                    {
                        Title = formatBackgroundTask.Title,
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

                var result = await formatCommand.Execute(context.Token);

                await Task.Delay(500, context.Token);

                OnProgressUpdated(new Progress
                {
                    Title = formatBackgroundTask.Title,
                    IsComplete = true,
                    HasError = result.IsFaulted,
                    ErrorMessage = result.IsFaulted ? result.Error.Message : null,
                    PercentComplete = 100
                });
            }
            catch (Exception e)
            {
                logger.LogError(e, "An unexpected error occured while executing format command");

                OnProgressUpdated(new Progress
                {
                    Title = formatBackgroundTask.Title,
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
