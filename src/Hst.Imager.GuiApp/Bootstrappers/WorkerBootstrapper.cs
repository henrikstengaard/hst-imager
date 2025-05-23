﻿namespace Hst.Imager.GuiApp.Bootstrappers
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using BackgroundTasks;
    using Extensions;
    using Helpers;
    using Hst.Imager.Core;
    using Hst.Imager.Core.Helpers;
    using Microsoft.AspNetCore.SignalR.Client;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Models;
    using Serilog;
    using Serilog.Events;
    using Services;
    using BackgroundTask = Core.Models.BackgroundTasks.BackgroundTask;

    public static class WorkerBootstrapper
    {
        public static async Task Start(string appDataPath, string baseUrl, int processId, bool hasDebugEnabled)
        {
#if RELEASE
            SetupReleaseLogging(appDataPath, hasDebugEnabled);
#else
            SetupDebugLogging(appDataPath);
#endif

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            var logger = serviceProvider.GetService<ILogger<Program>>();

            if (processId > 0)
            {
                KillOtherWorkers(logger, processId);
            }

            logger.LogDebug($"Connecting to base url '{baseUrl}'");

            var baseUri = new Uri(baseUrl);
            
            var progressHubConnection = new HubConnectionBuilder()
                .WithUrl(new Uri(baseUri, "hubs/progress"))
                .WithAutomaticReconnect()
                .ConfigureLogging(logging =>
                {
                    if (!hasDebugEnabled)
                    {
                        return;
                    }

                    logging.AddSerilog();
                })
                .Build();

            var errorHubConnection = new HubConnectionBuilder()
                .WithUrl(new Uri(baseUri, "hubs/error"))
                .WithAutomaticReconnect()
                .ConfigureLogging(logging =>
                {
                    if (!hasDebugEnabled)
                    {
                        return;
                    }

                    logging.AddSerilog();
                })
                .Build();

            var workerHubConnection = new HubConnectionBuilder()
                .WithUrl(new Uri(baseUri, "hubs/worker"))
                .WithAutomaticReconnect()
                .ConfigureLogging(logging =>
                {
                    if (!hasDebugEnabled)
                    {
                        return;
                    }

                    logging.AddSerilog();
                })
                .Build();

            var resultHubConnection = new HubConnectionBuilder()
                .WithUrl(new Uri(baseUri, "hubs/result"))
                .WithAutomaticReconnect()
                .ConfigureLogging(logging =>
                {
                    if (!hasDebugEnabled)
                    {
                        return;
                    }

                    logging.AddSerilog();
                })
                .Build();

            await progressHubConnection.StartAsync();
            await errorHubConnection.StartAsync();
            await workerHubConnection.StartAsync();
            await resultHubConnection.StartAsync();

            logger.LogDebug($"ProgressHubConnection = {progressHubConnection.State}");
            logger.LogDebug($"ErrorHubConnection = {errorHubConnection.State}");
            logger.LogDebug($"WorkerHubConnection = {workerHubConnection.State}");
            logger.LogDebug($"ResultHubConnection = {resultHubConnection.State}");

            var physicalDriveManagerFactory = new PhysicalDriveManagerFactory(loggerFactory);

            var backgroundTaskQueue = new BackgroundTaskQueue(100);
            var activeBackgroundTaskList = new ActiveBackgroundTaskList();
            var queuedHostedService = new QueuedHostedService(backgroundTaskQueue, activeBackgroundTaskList,
                loggerFactory.CreateLogger<QueuedHostedService>());
            var appState = AppState.Create(appDataPath, baseUrl, true);

            var backgroundTaskHandler = new BackgroundTaskHandler(
                loggerFactory.CreateLogger<BackgroundTaskHandler>(),
                loggerFactory,
                progressHubConnection,
                errorHubConnection,
                resultHubConnection,
                physicalDriveManagerFactory.Create(),
                activeBackgroundTaskList,
                backgroundTaskQueue,
                appState);

            await queuedHostedService.StartAsync(CancellationToken.None);

            workerHubConnection.On<BackgroundTask>(Core.Models.Constants.HubMethodNames.RunBackgroundTask, async (task) =>
            {
                logger.LogDebug($"Start handle background task type '{task.Type}' with payload '{task.Payload}'");
                try
                {
                    await backgroundTaskHandler.Handle(task);
                    logger.LogDebug($"End handle background task type '{task.Type}' with payload '{task.Payload}'");
                }
                catch (Exception e)
                {
                    logger.LogError(e,
                        $"Failed to handle background task '{task.Type}' with payload '{task.Payload}'");
                }
            });

            workerHubConnection.On(Core.Models.Constants.HubMethodNames.CancelBackgroundTask, () =>
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
            });

            var workerProcessId = Process.GetCurrentProcess().Id;
            logger.LogDebug($"Worker process id '{workerProcessId}'");
            
            await workerHubConnection.WorkerProcess(workerProcessId);
            
            logger.LogDebug("Worker is ready");

            var pingFailed = 0;
            var maxPingFailed = 3;
            while (true)
            {
                await Task.Delay(5000);

                try
                {
                    await workerHubConnection.WorkerPing();
                    pingFailed = 0;
                }
                catch (Exception)
                {
                    pingFailed++;
                }

                if (pingFailed <= maxPingFailed)
                {
                    continue;
                }
                logger.LogInformation($"Stopping worker after ping failed {maxPingFailed} times");
                return;
            }
        }

        private static void KillOtherWorkers(ILogger<Program> logger, int processId)
        {
            logger.LogDebug($"Killing other workers except process id = '{processId}'");

            var executingFile = WorkerHelper.GetExecutingFile();
            var workerFileName = WorkerHelper.GetWorkerFileName(executingFile);
            var currentProcessId = Process.GetCurrentProcess().Id;
            var processes = Process.GetProcesses();

            foreach (var process in processes)
            {
                try
                {
                    if (process.Id == currentProcessId ||
                        process.Id == processId ||
                        process.ProcessName.IndexOf(workerFileName,
                            StringComparison.OrdinalIgnoreCase) < 0 ||
                        process.MainModule == null ||
                        process.MainModule.FileName == null ||
                        process.MainModule.FileName.IndexOf(workerFileName, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                }
                catch (Exception)
                {
                    continue;
                }

                logger.LogDebug($"Killing worker process id '{process.Id}', name '{process.ProcessName}'");
                process.Kill();
            }
        }

        private static void SetupReleaseLogging(string appDataPath, bool hasDebugEnabled)
        {
            var logFilePath = Path.Combine(appDataPath, "logs", "log-worker.txt");
            if (hasDebugEnabled)
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .WriteTo.File(
                        logFilePath,
                        rollingInterval: RollingInterval.Day,
                        outputTemplate:
                        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] ({SourceContext}) {Message}{NewLine}{Exception}")
                    .WriteTo.Console()
                    .CreateLogger();
                return;
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    logFilePath,
                    rollingInterval: RollingInterval.Day,
                    outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] ({SourceContext}) {Message}{NewLine}{Exception}")
                .CreateLogger();
        }

        private static void SetupDebugLogging(string appDataPath)
        {
            var logFilePath = Path.Combine(appDataPath, "logs", "log-worker.txt");
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .WriteTo.File(
                    logFilePath,
                    rollingInterval: RollingInterval.Day,
                    outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] ({SourceContext}) {Message}{NewLine}{Exception}")
                .WriteTo.Console()
                .CreateLogger();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(configure => configure.AddSerilog());
        }
    }
}