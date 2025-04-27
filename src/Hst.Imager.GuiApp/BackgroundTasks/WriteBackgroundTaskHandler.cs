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

    public class WriteBackgroundTaskHandler(
        ILoggerFactory loggerFactory,
        IPhysicalDriveManager physicalDriveManager,
        AppState appState)
        : IBackgroundTaskHandler
    {
        private readonly ILogger<WriteBackgroundTaskHandler> logger = loggerFactory.CreateLogger<WriteBackgroundTaskHandler>();

        public event EventHandler<ProgressEventArgs> ProgressUpdated;

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not WriteBackgroundTask writeBackgroundTask)
            {
                return;
            }

            try
            {
                var physicalDrives = await physicalDriveManager.GetPhysicalDrives(
                    appState.Settings.AllPhysicalDrives);

                using var commandHelper = new CommandHelper(loggerFactory.CreateLogger<ICommandHelper>(), appState.IsAdministrator);
                var writeCommand =
                    new WriteCommand(loggerFactory.CreateLogger<WriteCommand>(), commandHelper, physicalDrives,
                        string.Concat(writeBackgroundTask.Byteswap ? "+bs:" : string.Empty,
                            writeBackgroundTask.SourcePath),
                        writeBackgroundTask.DestinationPath, new Size(writeBackgroundTask.Size, Unit.Bytes), 
                        appState.Settings.Retries, appState.Settings.Verify, appState.Settings.Force,
                        appState.Settings.SkipUnusedSectors,  writeBackgroundTask.StartOffset);
                writeCommand.DataProcessed += (_, args) =>
                {
                    OnProgressUpdated(new Progress
                    {
                        Title = writeBackgroundTask.Title,
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

                var result = await writeCommand.Execute(context.Token);

                OnProgressUpdated(new Progress
                {
                    Title = writeBackgroundTask.Title,
                    IsComplete = true,
                    HasError = result.IsFaulted,
                    ErrorMessage = result.IsFaulted ? result.Error.Message : null,
                    PercentComplete = 100
                });
            }
            catch (Exception e)
            {
                logger.LogError(e, "An unexpected error occured while executing write command");

                OnProgressUpdated(new Progress
                {
                    Title = writeBackgroundTask.Title,
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