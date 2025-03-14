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

    public class WriteBackgroundTaskHandler : IBackgroundTaskHandler
    {
        private readonly ILogger<WriteBackgroundTaskHandler> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly HubConnection progressHubConnection;
        private readonly IPhysicalDriveManager physicalDriveManager;
        private readonly AppState appState;

        public WriteBackgroundTaskHandler(
            ILoggerFactory loggerFactory,
            HubConnection progressHubConnection,
            IPhysicalDriveManager physicalDriveManager,
            AppState appState)
        {
            this.logger = loggerFactory.CreateLogger<WriteBackgroundTaskHandler>();
            this.loggerFactory = loggerFactory;
            this.progressHubConnection = progressHubConnection;
            this.physicalDriveManager = physicalDriveManager;
            this.appState = appState;
        }

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not WriteBackgroundTask writeBackgroundTask)
            {
                return;
            }

            try
            {
                var physicalDrives = await physicalDriveManager.GetPhysicalDrives(
                    appState.Settings.AllPhysicalDrives);

                var commandHelper = new CommandHelper(this.loggerFactory.CreateLogger<ICommandHelper>(), appState.IsAdministrator);
                var writeCommand =
                    new WriteCommand(loggerFactory.CreateLogger<WriteCommand>(), commandHelper, physicalDrives,
                        writeBackgroundTask.Byteswap 
                            ? System.IO.Path.Combine(writeBackgroundTask.SourcePath, "+bs")
                            : writeBackgroundTask.SourcePath,
                        writeBackgroundTask.DestinationPath, new Size(writeBackgroundTask.Size, Unit.Bytes), 
                        appState.Settings.Retries, appState.Settings.Verify, appState.Settings.Force,
                        appState.Settings.SkipUnusedSectors);
                writeCommand.DataProcessed += async (_, args) =>
                {
                    await progressHubConnection.UpdateProgress(new Progress
                    {
                        Title = writeBackgroundTask.Title,
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

                var result = await writeCommand.Execute(context.Token);

                await progressHubConnection.UpdateProgress(new Progress
                {
                    Title = writeBackgroundTask.Title,
                    IsComplete = true,
                    HasError = result.IsFaulted,
                    ErrorMessage = result.IsFaulted ? result.Error.Message : null,
                    PercentComplete = 100
                }, context.Token);
            }
            catch (Exception e)
            {
                logger.LogError(e, "An unexpected error occured while executing write command");

                await progressHubConnection.UpdateProgress(new Progress
                {
                    Title = writeBackgroundTask.Title,
                    IsComplete = true,
                    HasError = true,
                    ErrorMessage = e.Message,
                    PercentComplete = 100
                }, context.Token);
            }
        }
    }
}