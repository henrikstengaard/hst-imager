using System;
using System.Linq;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;

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
        private readonly ILogger<ListBackgroundTaskHandler> logger = loggerFactory.CreateLogger<ListBackgroundTaskHandler>();

        public event EventHandler<MediaInfoEventArgs> MediaInfoRead;
        public event EventHandler<ErrorEventArgs> ErrorOccurred;

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not InfoBackgroundTask infoBackgroundTask)
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
                var infoCommand = new InfoCommand(loggerFactory.CreateLogger<InfoCommand>(), commandHelper, physicalDrives,
                    string.Concat(infoBackgroundTask.Byteswap ? "+bs:" : string.Empty, infoBackgroundTask.Path),
                        infoBackgroundTask.AllowNonExisting);

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
            catch (Exception e)
            {
                logger.LogError(e, "An unexpected error occured while executing list command");

                OnErrorOccurred(e.Message);
            }        }
        
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