namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System;
    using System.Text.Json;
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
            if (activeBackgroundTaskList.Count > 0)
            {
                logger.LogDebug("Cancel background task");
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
            //var task = JsonSerializer.Deserialize(backgroundTask.Payload, Type.GetType(backgroundTask.Type));

            //Console.WriteLine(task == null ? "task is null" : task.GetType().FullName);
            switch (backgroundTask.Type)
            {
                case nameof(InfoBackgroundTask):
                    return JsonSerializer.Deserialize<InfoBackgroundTask>(backgroundTask.Payload);
                case nameof(ListBackgroundTask):
                    return JsonSerializer.Deserialize<ListBackgroundTask>(backgroundTask.Payload);
                case nameof(ReadBackgroundTask):
                    return JsonSerializer.Deserialize<ReadBackgroundTask>(backgroundTask.Payload);
                case nameof(WriteBackgroundTask):
                    return JsonSerializer.Deserialize<WriteBackgroundTask>(backgroundTask.Payload);
                case nameof(CompareBackgroundTask):
                    return JsonSerializer.Deserialize<CompareBackgroundTask>(backgroundTask.Payload);
                case nameof(ConvertBackgroundTask):
                    return JsonSerializer.Deserialize<ConvertBackgroundTask>(backgroundTask.Payload);
                case nameof(BlankBackgroundTask):
                    return JsonSerializer.Deserialize<BlankBackgroundTask>(backgroundTask.Payload);
                case nameof(OptimizeBackgroundTask):
                    return JsonSerializer.Deserialize<OptimizeBackgroundTask>(backgroundTask.Payload);
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
                _ => null
            };
        }
    }
}