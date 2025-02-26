﻿namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Core;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Microsoft.AspNetCore.SignalR.Client;
    using Microsoft.Extensions.Logging;
    using Models;
    using Services;
    using BackgroundTask = Core.Models.BackgroundTasks.BackgroundTask;

    public class BackgroundTaskHandler
    {
        private readonly ILogger<BackgroundTaskHandler> logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly HubConnection progressHubConnection;
        private readonly HubConnection errorHubConnection;
        private readonly HubConnection resultHubConnection;
        private readonly IPhysicalDriveManager physicalDriveManager;
        private readonly ActiveBackgroundTaskList activeBackgroundTaskList;
        private readonly BackgroundTaskQueue backgroundTaskQueue;
        private readonly AppState appState;

        private static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        public BackgroundTaskHandler(ILogger<BackgroundTaskHandler> logger,
            ILoggerFactory loggerFactory,
            HubConnection progressHubConnection,
            HubConnection errorHubConnection,
            HubConnection resultHubConnection,
            IPhysicalDriveManager physicalDriveManager,
            ActiveBackgroundTaskList activeBackgroundTaskList,
            BackgroundTaskQueue backgroundTaskQueue, AppState appState)
        {
            this.logger = logger;
            this.loggerFactory = loggerFactory;
            this.progressHubConnection = progressHubConnection;
            this.errorHubConnection = errorHubConnection;
            this.resultHubConnection = resultHubConnection;
            this.physicalDriveManager = physicalDriveManager;
            this.activeBackgroundTaskList = activeBackgroundTaskList;
            this.backgroundTaskQueue = backgroundTaskQueue;
            this.appState = appState;
        }

        public async Task Handle(BackgroundTask backgroundTask)
        {
            if (activeBackgroundTaskList.Count > 0 && backgroundTask.CancelAll)
            {
                logger.LogDebug($"Background task '{backgroundTask.Type}' requests cancel all");
                try
                {
                    activeBackgroundTaskList.CancelAll();
                }
                catch (Exception e)
                {
                    logger.LogError(e,
                        "Failed to cancel background task");
                }
            }

            var task = ResolveTask(backgroundTask);

            if (task == null)
            {
                logger.LogError($"Unable to resolve background task '{backgroundTask.Type}'");
                return;
            }

            logger.LogDebug($"Resolved background task '{task.GetType().FullName}'");

            var handler = ResolveHandler(task);

            if (handler == null)
            {
                logger.LogError($"Unable to resolve handler background task '{backgroundTask.Type}'");
                return;
            }

            logger.LogDebug($"Resolved background task handler '{handler.GetType().FullName}'");

            await backgroundTaskQueue.QueueBackgroundWorkItemAsync(handler.Handle, task);
        }

        private IBackgroundTask ResolveTask(BackgroundTask backgroundTask)
        {
            switch (backgroundTask.Type)
            {
                case nameof(InfoBackgroundTask):
                    return JsonSerializer.Deserialize<InfoBackgroundTask>(backgroundTask.Payload, JsonSerializerOptions);
                case nameof(ListBackgroundTask):
                    return JsonSerializer.Deserialize<ListBackgroundTask>(backgroundTask.Payload, JsonSerializerOptions);
                case nameof(ReadBackgroundTask):
                    return JsonSerializer.Deserialize<ReadBackgroundTask>(backgroundTask.Payload, JsonSerializerOptions);
                case nameof(WriteBackgroundTask):
                    return JsonSerializer.Deserialize<WriteBackgroundTask>(backgroundTask.Payload, JsonSerializerOptions);
                case nameof(CompareBackgroundTask):
                    return JsonSerializer.Deserialize<CompareBackgroundTask>(backgroundTask.Payload, JsonSerializerOptions);
                case nameof(ConvertBackgroundTask):
                    return JsonSerializer.Deserialize<ConvertBackgroundTask>(backgroundTask.Payload, JsonSerializerOptions);
                case nameof(BlankBackgroundTask):
                    return JsonSerializer.Deserialize<BlankBackgroundTask>(backgroundTask.Payload, JsonSerializerOptions);
                case nameof(OptimizeBackgroundTask):
                    return JsonSerializer.Deserialize<OptimizeBackgroundTask>(backgroundTask.Payload, JsonSerializerOptions);
                case nameof(FormatBackgroundTask):
                    return JsonSerializer.Deserialize<FormatBackgroundTask>(backgroundTask.Payload, JsonSerializerOptions);
                default:
                    logger.LogError($"Background task '{backgroundTask.Type}' not supported");
                    return null;
            }
        }

        private IBackgroundTaskHandler ResolveHandler(IBackgroundTask backgroundTask)
        {
            return backgroundTask switch
            {
                ListBackgroundTask => new ListBackgroundTaskHandler(loggerFactory, resultHubConnection,
                    errorHubConnection, physicalDriveManager, appState),
                InfoBackgroundTask => new InfoBackgroundTaskHandler(loggerFactory,
                    resultHubConnection, errorHubConnection, physicalDriveManager, appState),
                ReadBackgroundTask => new ReadBackgroundTaskHandler(loggerFactory, progressHubConnection,
                    physicalDriveManager, appState),
                WriteBackgroundTask => new WriteBackgroundTaskHandler(loggerFactory, progressHubConnection,
                    physicalDriveManager, appState),
                CompareBackgroundTask => new CompareBackgroundTaskHandler(loggerFactory,
                    progressHubConnection, physicalDriveManager, appState),
                FormatBackgroundTask => new FormatBackgroundTaskHandler(loggerFactory,
                    progressHubConnection, physicalDriveManager, appState),
                _ => null
            };
        }
    }
}