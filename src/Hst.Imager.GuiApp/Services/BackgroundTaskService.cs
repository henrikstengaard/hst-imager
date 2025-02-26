namespace Hst.Imager.GuiApp.Services
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Hubs;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public class BackgroundTaskService : BackgroundService
    {
        private readonly ILogger<BackgroundTaskService> logger;
        private readonly IHubContext<WorkerHub> workerHubContext;
        private readonly IHubContext<ErrorHub> errorHubContext;
        private readonly WorkerService workerService;
        private const int RetryWaitMilliseconds = 1000;
        private const int MaxWaitMilliseconds = 60000;

        public BackgroundTaskService(
            ILoggerFactory loggerFactory,
            IServiceScopeFactory serviceScopeFactory)
        {
            logger = loggerFactory.CreateLogger<BackgroundTaskService>();
            using var scope = serviceScopeFactory.CreateScope();
            workerService = scope.ServiceProvider.GetService<WorkerService>();
            workerHubContext = scope.ServiceProvider.GetService<IHubContext<WorkerHub>>();
            errorHubContext = scope.ServiceProvider.GetService<IHubContext<ErrorHub>>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogDebug("Starting background task service");

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), stoppingToken);

                try
                {
                    logger.LogDebug("Dequeued run background task");
                    
                    var backgroundTasks = (await workerService.DequeueAsync()).ToList();

                    if (!backgroundTasks.Any())
                    {
                        continue;
                    }

                    if (!workerService.IsRunning() || !workerService.IsReady())
                    {
                        logger.LogDebug($"Worker is not running or not ready");
                        var result = await workerService.Start();
                        if (result.IsFaulted)
                        {
                            logger.LogError(result.Error.ToString());
                            await errorHubContext.SendError(result.Error.ToString(), token: stoppingToken);
                            continue;
                        }

                        var count = 0;
                        var maxRetries = MaxWaitMilliseconds / RetryWaitMilliseconds;
                        var hasFailedToStartWorker = false;
                        while (!workerService.IsReady())
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(RetryWaitMilliseconds), stoppingToken);
                            count++;
                            if (count <= maxRetries)
                            {
                                continue;
                            }
                            hasFailedToStartWorker = true;
                            break;
                        }

                        if (hasFailedToStartWorker)
                        {
                            var message = "Worker failed to start after waiting it to be ready";
                            logger.LogError(message);
                            await errorHubContext.SendError(message, token: stoppingToken);
                            continue;
                        }
                    }
                    
                    logger.LogDebug($"Worker hub run {backgroundTasks.Count} background tasks");

                    foreach (var backgroundTask in backgroundTasks)
                    {
                        await workerHubContext.RunBackgroundTask(backgroundTask, token: stoppingToken);
                    }
                }
                catch (Exception e)
                {
                    var message = "Failed to dequeue and run background tasks";
                    logger.LogError(e, message);
                    await errorHubContext.SendError($"{message}: {e.Message}", token: stoppingToken);
                }
            }
        }
    }
}