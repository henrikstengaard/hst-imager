namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System.Linq;
    using System.Threading.Tasks;
    using Extensions;
    using Core;
    using Core.Commands;
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
            var physicalDrives = (await physicalDriveManager.GetPhysicalDrives(
                appState.Settings.AllPhysicalDrives)).ToList();

            var commandHelper = new CommandHelper(this.loggerFactory.CreateLogger<ICommandHelper>(), appState.IsAdministrator);
            var logger = loggerFactory.CreateLogger<InfoCommand>();
            var listCommand = new ListCommand(loggerFactory.CreateLogger<ListCommand>(), commandHelper, physicalDrives);

            listCommand.ListRead += async (_, args) =>
            {
                await resultHubConnection.SendListResult(args.MediaInfos.Select(x => x.ToViewModel()).ToList());
            };

            var result = await listCommand.Execute(context.Token);
            if (result == null)
            {
                logger.LogError("List command returned null");
                return;
            }
            if (result.IsFaulted)
            {
                var message = result.Error?.Message ?? "List command returned error without message error";
                logger.LogError(message);
                await errorHubConnection.UpdateError(message, context.Token);
            }
        }
    }
}