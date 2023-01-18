﻿namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System;
    using System.Threading.Tasks;
    using Extensions;
    using Core;
    using Core.Commands;
    using Core.Models;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Microsoft.AspNetCore.SignalR.Client;
    using Microsoft.Extensions.Logging;
    using Models;

    public class PhysicalDriveVerifyBackgroundTaskHandler : IBackgroundTaskHandler
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly HubConnection progressHubConnection;
        private readonly IPhysicalDriveManager physicalDriveManager;
        private readonly AppState appState;

        public PhysicalDriveVerifyBackgroundTaskHandler(
            ILoggerFactory loggerFactory,
            HubConnection progressHubConnection, IPhysicalDriveManager physicalDriveManager, AppState appState)
        {
            this.loggerFactory = loggerFactory;
            this.progressHubConnection = progressHubConnection;
            this.physicalDriveManager = physicalDriveManager;
            this.appState = appState;
        }

        public async ValueTask Handle(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not PhysicalDriveVerifyBackgroundTask verifyBackgroundTask)
            {
                return;
            }

            try
            {
                var physicalDrives = await physicalDriveManager.GetPhysicalDrives();

                var commandHelper = new CommandHelper(appState.IsAdministrator);
                var verifyCommand =
                    new CompareCommand(loggerFactory.CreateLogger<CompareCommand>(), commandHelper, physicalDrives,
                        verifyBackgroundTask.SourcePath,
                        verifyBackgroundTask.DestinationPath, new Size(), 0, false);
                verifyCommand.DataProcessed += async (_, args) =>
                {
                    await progressHubConnection.UpdateProgress(new Progress
                    {
                        Title = verifyBackgroundTask.Title,
                        IsComplete = false,
                        PercentComplete = args.PercentComplete,
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

                var result = await verifyCommand.Execute(context.Token);

                await progressHubConnection.UpdateProgress(new Progress
                {
                    Title = verifyBackgroundTask.Title,
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
                    Title = verifyBackgroundTask.Title,
                    IsComplete = true,
                    HasError = true,
                    ErrorMessage = e.Message,
                    PercentComplete = 100
                }, context.Token);
            }
        }
    }
}