using System;

namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System.Threading.Tasks;
    using Core;
    using Core.Commands;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Microsoft.Extensions.Logging;
    using Models;

    public class InfoBackgroundTaskHandler(
        ILoggerFactory loggerFactory,
        IPhysicalDriveManager physicalDriveManager,
        AppState appState)
        : IBackgroundTaskHandler
    {
        public event EventHandler<MediaInfoEventArgs> MediaInfoRead;
        public event EventHandler<ErrorEventArgs> ErrorOccurred;

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not InfoBackgroundTask infoBackgroundTask)
            {
                return;
            }

            var physicalDrives = await physicalDriveManager.GetPhysicalDrives(
                appState.Settings.AllPhysicalDrives);

            using var commandHelper = new CommandHelper(loggerFactory.CreateLogger<ICommandHelper>(), appState.IsAdministrator);
            var logger = loggerFactory.CreateLogger<InfoCommand>();
            var infoCommand = new InfoCommand(logger, commandHelper, physicalDrives,
                string.Concat(infoBackgroundTask.Byteswap ? "+bs:" : string.Empty, infoBackgroundTask.Path));

            infoCommand.DiskInfoRead += (_, args) =>
            {
                OnMediaInfoRead(args.MediaInfo);
            };

            var result = await infoCommand.Execute(context.Token);
            if (result.IsFaulted)
            {
                // send null info result for views to reset/clear
                OnMediaInfoRead(null);

                var message = result.Error?.Message ?? "Info command returned error without message error";
                logger.LogError(message);
                
                OnErrorOccurred(message);
            }
        }
        
        private void OnMediaInfoRead(MediaInfo mediaInfo)
        {
            MediaInfoRead?.Invoke(this, new MediaInfoEventArgs(mediaInfo));
        }
        
        private void OnErrorOccurred(string errorMessage)
        {
            ErrorOccurred?.Invoke(this, new ErrorEventArgs(errorMessage));
        }
    }
}