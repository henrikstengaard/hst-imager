using System;
using System.Collections.Generic;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;

namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System.Linq;
    using System.Threading.Tasks;
    using Core;
    using Core.Commands;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Microsoft.Extensions.Logging;
    using Models;

    public class ListBackgroundTaskHandler(
        ILoggerFactory loggerFactory,
        IPhysicalDriveManager physicalDriveManager,
        AppState appState)
        : IBackgroundTaskHandler
    {
        private readonly ILogger<ListBackgroundTaskHandler> logger = loggerFactory.CreateLogger<ListBackgroundTaskHandler>();

        public event EventHandler<ListReadEventArgs> ListRead;
        public event EventHandler<ErrorEventArgs> ErrorOccurred;

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not ListBackgroundTask)
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
                var listCommand = new ListCommand(loggerFactory.CreateLogger<ListCommand>(), commandHelper, physicalDrives);

                listCommand.ListRead += (_, args) =>
                {
                    OnListRead(args.MediaInfos.Where(x => !x.SystemDrive));
                };

                var result = await listCommand.Execute(context.Token);
                if (result.IsFaulted)
                {
                    // send empty list result for views to reset/clear
                    OnListRead([]);

                    var message = result.Error?.Message ?? "List command returned error without message error";
                    logger.LogError(message);
                    
                    OnErrorOccurred(message);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "An unexpected error occured while executing list command");

                OnErrorOccurred(e.Message);
            }
        }
        
        private void OnListRead(IEnumerable<MediaInfo> mediaInfos)
        {
            ListRead?.Invoke(this, new ListReadEventArgs(mediaInfos));
        }
        
        private void OnErrorOccurred(string errorMessage)
        {
            ErrorOccurred?.Invoke(this, new ErrorEventArgs(errorMessage));
        }
    }
}