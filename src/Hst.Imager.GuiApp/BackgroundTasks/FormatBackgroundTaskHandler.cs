using Hst.Imager.Core;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.BackgroundTasks;
using Hst.Imager.Core.Models;
using Hst.Imager.GuiApp.Extensions;
using Hst.Imager.GuiApp.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;
using Hst.Imager.GuiApp.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Hst.Imager.GuiApp.BackgroundTasks
{
    public class FormatBackgroundTaskHandler : IBackgroundTaskHandler
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly HubConnection progressHubConnection;
        private readonly IPhysicalDriveManager physicalDriveManager;
        private readonly AppState appState;

        public FormatBackgroundTaskHandler(
            ILoggerFactory loggerFactory,
            HubConnection progressHubConnection,
            IPhysicalDriveManager physicalDriveManager,
            AppState appState)
        {
            this.loggerFactory = loggerFactory;
            this.progressHubConnection = progressHubConnection;
            this.physicalDriveManager = physicalDriveManager;
            this.appState = appState;
        }

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not FormatBackgroundTask formatBackgroundTask)
            {
                return;
            }

            try
            {
                await progressHubConnection.UpdateProgress(new Progress
                {
                    Title = formatBackgroundTask.Title,
                    IsComplete = false,
                    HasError = false,
                    ErrorMessage = null,
                    PercentComplete = 0
                }, context.Token);

                var physicalDrives = await physicalDriveManager.GetPhysicalDrives();

                var commandHelper = new CommandHelper(this.loggerFactory.CreateLogger<ICommandHelper>(), appState.IsAdministrator);
                var formatCommand = new FormatCommand(loggerFactory.CreateLogger<FormatCommand>(), loggerFactory,
                    commandHelper, physicalDrives,
                    string.Concat(formatBackgroundTask.Path, formatBackgroundTask.Byteswap ? "+bs" : string.Empty),
                    formatBackgroundTask.FormatType, formatBackgroundTask.FileSystem, 
                    formatBackgroundTask.FileSystemPath,
                    appState.AppDataPath,
                    new Size(formatBackgroundTask.Size, Unit.Bytes));
                formatCommand.DataProcessed += async (_, args) =>
                {
                    await progressHubConnection.UpdateProgress(new Progress
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
                            : new long?(),
                        MillisecondsRemaining = args.PercentComplete > 0
                            ? (long)args.TimeRemaining.TotalMilliseconds
                            : new long?(),
                        MillisecondsTotal = args.PercentComplete > 0
                            ? (long)args.TimeTotal.TotalMilliseconds
                            : new long?()
                    }, context.Token);
                };

                var result = await formatCommand.Execute(context.Token);

                await Task.Delay(500, context.Token);

                await progressHubConnection.UpdateProgress(new Progress
                {
                    Title = formatBackgroundTask.Title,
                    IsComplete = true,
                    HasError = result.IsFaulted,
                    ErrorMessage = result.IsFaulted ? result.Error.Message : null,
                    PercentComplete = 100
                }, context.Token);
            }
            catch (Exception e)
            {
                await progressHubConnection.UpdateProgress(new Progress
                {
                    Title = formatBackgroundTask.Title,
                    IsComplete = true,
                    HasError = true,
                    ErrorMessage = e.Message,
                    PercentComplete = 100
                }, context.Token);
            }
        }
    }
}
