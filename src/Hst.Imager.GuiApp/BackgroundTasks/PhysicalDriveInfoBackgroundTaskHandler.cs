namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System.Threading.Tasks;
    using Extensions;
    using Hst.Imager.Core;
    using Hst.Imager.Core.Commands;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Microsoft.AspNetCore.SignalR.Client;
    using Microsoft.Extensions.Logging;
    using Models;

    public class PhysicalDriveInfoBackgroundTaskHandler : IBackgroundTaskHandler
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly HubConnection resultHubConnection;
        private readonly HubConnection errorHubConnection;
        private readonly IPhysicalDriveManager physicalDriveManager;
        private readonly AppState appState;

        public PhysicalDriveInfoBackgroundTaskHandler(ILoggerFactory loggerFactory, HubConnection resultHubConnection,
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
            if (context.BackgroundTask is not PhysicalDriveInfoBackgroundTask infoBackgroundTask)
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
                await errorHubConnection.UpdateError(result.Error.Message, context.Token);
            }
        }
    }
}