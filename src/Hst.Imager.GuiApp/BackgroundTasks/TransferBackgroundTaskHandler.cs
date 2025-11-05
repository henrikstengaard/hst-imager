using Hst.Imager.Core;

namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System;
    using System.Threading.Tasks;
    using Core.Commands;
    using Hst.Imager.Core.Models;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Microsoft.Extensions.Logging;
    using Models;

    public class TransferBackgroundTaskHandler(
        ILoggerFactory loggerFactory,
        AppState appState)
        : IBackgroundTaskHandler
    {
        private readonly ILogger<TransferBackgroundTaskHandler> logger = loggerFactory.CreateLogger<TransferBackgroundTaskHandler>();

        public event EventHandler<ProgressEventArgs> ProgressUpdated;

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not TransferBackgroundTask transferBackgroundTask)
            {
                return;
            }

            try
            {
                OnProgressUpdated(new Progress
                {
                    Title = transferBackgroundTask.Title,
                    IsComplete = false,
                    HasError = false,
                    ErrorMessage = null,
                    PercentComplete = 0
                });

                using var commandHelper = new CommandHelper(loggerFactory.CreateLogger<ICommandHelper>(), appState.IsAdministrator);
                var transferCommand =
                    new TransferCommand(commandHelper,
                        string.Concat(transferBackgroundTask.Byteswap ? "+bs:" : string.Empty, 
                            transferBackgroundTask.SourcePath),
                        transferBackgroundTask.DestinationPath, new Size(transferBackgroundTask.Size, Unit.Bytes),
                        false, transferBackgroundTask.SrcStartOffset, transferBackgroundTask.DestStartOffset);
                transferCommand.DataProcessed += (_, args) =>
                {
                    OnProgressUpdated(new Progress
                    {
                        Title = transferBackgroundTask.Title,
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

                var result = await transferCommand.Execute(context.Token);
            
                OnProgressUpdated(new Progress
                {
                    Title = transferBackgroundTask.Title,
                    IsComplete = true,
                    HasError = result.IsFaulted,
                    ErrorMessage = result.IsFaulted ? result.Error.Message : null,
                    PercentComplete = 100
                });
            }
            catch (Exception e)
            {
                logger.LogError(e, "An unexpected error occured while executing transfer command");

                OnProgressUpdated(new Progress
                {
                    Title = transferBackgroundTask.Title,
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