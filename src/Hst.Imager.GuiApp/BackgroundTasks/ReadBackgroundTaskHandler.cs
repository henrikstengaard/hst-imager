﻿namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System;
    using System.Threading.Tasks;
    using Extensions;
    using Core;
    using Core.Commands;
    using Hst.Imager.Core.Models;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Microsoft.AspNetCore.SignalR.Client;
    using Microsoft.Extensions.Logging;
    using Models;

    public class ReadBackgroundTaskHandler : IBackgroundTaskHandler
    {
        private readonly ILogger<ReadBackgroundTaskHandler> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly HubConnection progressHubConnection;
        private readonly IPhysicalDriveManager physicalDriveManager;
        private readonly AppState appState;

        public ReadBackgroundTaskHandler(
            ILoggerFactory loggerFactory,
            HubConnection progressHubConnection,
            IPhysicalDriveManager physicalDriveManager,
            AppState appState)
        {
            this.logger = loggerFactory.CreateLogger<ReadBackgroundTaskHandler>();
            this.loggerFactory = loggerFactory;
            this.progressHubConnection = progressHubConnection;
            this.physicalDriveManager = physicalDriveManager;
            this.appState = appState;
        }

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not ReadBackgroundTask readBackgroundTask)
            {
                return;
            }

            try
            {
                var physicalDrives = await physicalDriveManager.GetPhysicalDrives(
                    appState.Settings.AllPhysicalDrives);

                var commandHelper = new CommandHelper(this.loggerFactory.CreateLogger<ICommandHelper>(), appState.IsAdministrator);
                var readCommand =
                    new ReadCommand(loggerFactory.CreateLogger<ReadCommand>(), commandHelper, physicalDrives,
                        readBackgroundTask.Byteswap
                            ? System.IO.Path.Combine(readBackgroundTask.SourcePath, "+bs")
                            : readBackgroundTask.SourcePath,
                        readBackgroundTask.DestinationPath,
                        new Size(readBackgroundTask.Size, Unit.Bytes), appState.Settings.Retries,
                        appState.Settings.Verify, appState.Settings.Force, readBackgroundTask.StartOffset);
                readCommand.DataProcessed += async (_, args) =>
                {
                    await progressHubConnection.UpdateProgress(new Progress
                    {
                        Title = readBackgroundTask.Title,
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

                var result = await readCommand.Execute(context.Token);

                await progressHubConnection.UpdateProgress(new Progress
                {
                    Title = readBackgroundTask.Title,
                    IsComplete = true,
                    HasError = result.IsFaulted,
                    ErrorMessage = result.IsFaulted ? result.Error.Message : null,
                    PercentComplete = 100
                }, context.Token);
            }
            catch (Exception e)
            {
                logger.LogError(e, "An unexpected error occured while executing read command");

                await progressHubConnection.UpdateProgress(new Progress
                {
                    Title = readBackgroundTask.Title,
                    IsComplete = true,
                    HasError = true,
                    ErrorMessage = e.Message,
                    PercentComplete = 100
                }, context.Token);
            }
        }
    }
}