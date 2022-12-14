namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System.Linq;
    using System.Threading.Tasks;
    using Extensions;
    using Hst.Imager.Core;
    using Hst.Imager.Core.Commands;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Microsoft.AspNetCore.SignalR.Client;
    using Microsoft.Extensions.Logging;
    using Models;

    public class ListBackgroundTaskHandler : IBackgroundTaskHandler
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly HubConnection resultHubConnection;
        private readonly HubConnection errorHubConnection;
        private readonly IPhysicalDriveManager physicalDriveManager;
        private readonly AppState appState;

        public ListBackgroundTaskHandler(
            ILoggerFactory loggerFactory,
            HubConnection resultHubConnection,
            HubConnection errorHubConnection,
            IPhysicalDriveManager physicalDriveManager, AppState appState)
        {
            this.loggerFactory = loggerFactory;
            this.resultHubConnection = resultHubConnection;
            this.errorHubConnection = errorHubConnection;
            this.physicalDriveManager = physicalDriveManager;
            this.appState = appState;
        }

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            var physicalDrives = await physicalDriveManager.GetPhysicalDrives();

            var commandHelper = new CommandHelper(appState.IsAdministrator);
            var listCommand = new ListCommand(loggerFactory.CreateLogger<ListCommand>(), commandHelper, physicalDrives);

            listCommand.ListRead += async (_, args) =>
            {
                await resultHubConnection.SendListResult(args.MediaInfos.Select(x => x.ToViewModel()).ToList());
            };

            var result = await listCommand.Execute(context.Token);
            if (result.IsFaulted)
            {
                await errorHubConnection.UpdateError(result.Error.Message, context.Token);
            }
        }
    }
}