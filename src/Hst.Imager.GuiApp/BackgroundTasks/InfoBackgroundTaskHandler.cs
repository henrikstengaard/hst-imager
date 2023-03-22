namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System.Threading.Tasks;
    using Extensions;
    using Core;
    using Core.Commands;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Microsoft.AspNetCore.SignalR.Client;
    using Microsoft.Extensions.Logging;
    using Models;

    public class InfoBackgroundTaskHandler : IBackgroundTaskHandler
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly HubConnection resultHubConnection;
        private readonly HubConnection errorHubConnection;
        private readonly IPhysicalDriveManager physicalDriveManager;
        private readonly AppState appState;

        public InfoBackgroundTaskHandler(ILoggerFactory loggerFactory, HubConnection resultHubConnection,
            HubConnection errorHubConnection,
            IPhysicalDriveManager physicalDriveManager, AppState appState)
        {
            this.resultHubConnection = resultHubConnection;
            this.errorHubConnection = errorHubConnection;
            this.physicalDriveManager = physicalDriveManager;
            this.appState = appState;
            this.loggerFactory = loggerFactory;
        }

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not InfoBackgroundTask infoBackgroundTask)
            {
                return;
            }

            var physicalDrives = await physicalDriveManager.GetPhysicalDrives();

            var commandHelper = new CommandHelper(appState.IsAdministrator);
            var logger = loggerFactory.CreateLogger<InfoCommand>();
            var infoCommand = new InfoCommand(logger, commandHelper, physicalDrives, infoBackgroundTask.Path);

            infoCommand.DiskInfoRead += async (_, args) =>
            {
                await resultHubConnection.SendInfoResult(args.MediaInfo.ToViewModel());
            };

            var result = await infoCommand.Execute(context.Token);
            if (result.IsFaulted)
            {
                // send null info result for views to reset/clear
                await resultHubConnection.SendInfoResult(null);
                
                var message = result.Error?.Message ?? "Info command returned error without message error";
                logger.LogError(message);
                await errorHubConnection.UpdateError(message, context.Token);
            }
        }
    }
}