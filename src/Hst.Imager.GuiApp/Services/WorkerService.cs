namespace Hst.Imager.GuiApp.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Helpers;
    using Hst.Core;
    using Hst.Imager.Core.Helpers;
    using Hst.Imager.Core.Models;
    using Hubs;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;
    using Models;

    public class WorkerService
    {
        private readonly ILogger<WorkerService> logger;
        private readonly AppState appState;
        private readonly IHubContext<ErrorHub> errorHubContext;
        private readonly BlockingCollection<Hst.Imager.Core.Models.BackgroundTasks.BackgroundTask> queue;
        private static readonly object LockObject = new();
        
        private int workerProcessId;

        public WorkerService(ILogger<WorkerService> logger, AppState appState, IHubContext<ErrorHub> errorHubContext)
        {
            this.logger = logger;
            this.appState = appState;
            this.errorHubContext = errorHubContext;
            this.queue = new BlockingCollection<Hst.Imager.Core.Models.BackgroundTasks.BackgroundTask>(new ConcurrentQueue<Hst.Imager.Core.Models.BackgroundTasks.BackgroundTask>());
            this.workerProcessId = 0;
        }

        public bool IsRunning()
        {
            lock (LockObject)
            {
                if (workerProcessId == 0)
                {
                    return false;
                }

                try
                {
                    var process = Process.GetProcessById(workerProcessId);
                    if (process.HasExited)
                    {
                        logger.LogDebug($"Worker process id {workerProcessId} has exited");
                        SetWorkerProcessId(0);
                        return false;
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e, $"Failed to get worker process id {workerProcessId}");
                    SetWorkerProcessId(0);
                    return false;
                }

                logger.LogDebug($"Worker process id {workerProcessId} is running");
                return true;
            }
        }

        public async Task<Result<bool>> Start()
        {
            var settings = await ApplicationDataHelper.ReadSettings<Settings>(Constants.AppName) ?? new Settings();
            
            var workerCommand = WorkerHelper.GetWorkerFileName(appState.ExecutingFile);
            var workerPath = Path.Combine(
                appState.AppPath,
                workerCommand);

            logger.LogDebug($"Worker path = '{workerPath}'");
            
            if (!File.Exists(workerPath))
            {
                return new Result<bool>(new Error($"Failed to start worker '{workerPath}'. Path not found!"));
            }

            var currentProcessId = Process.GetCurrentProcess().Id;
            var arguments = $"--worker --baseurl {appState.BaseUrl} --process-id {currentProcessId}";
            logger.LogDebug($"Starting worker '{workerPath}' with arguments '{arguments}'");

            var processStartInfo = ElevateHelper.GetElevatedProcessStartInfo(
                $"{Constants.AppName} needs administrator privileges for raw disk access", workerCommand, arguments,
                appState.AppPath, Debugger.IsAttached || ApplicationDataHelper.HasDebugEnabled(Constants.AppName), 
                settings.MacOsElevateMethod == Settings.MacOsElevateMethodEnum.OsascriptSudo);

            logger.LogDebug($"Worker process file name '{processStartInfo.FileName}' with arguments '{processStartInfo.Arguments}'");
            
            var workerProcess = ElevateHelper.StartElevatedProcess(processStartInfo);

            if (!workerProcess.HasExited || workerProcess.ExitCode == 0)
            {
                return new Result<bool>(true);
            }
            
            return new Result<bool>(new Error(
                $"Failed to start worker '{workerPath}'. Process exited with error code {workerProcess.ExitCode}"));
        }

        public Task EnqueueAsync<T>(IEnumerable<T> backgroundTasks, bool cancelAll = false)
        {
            if (backgroundTasks == null)
            {
                throw new ArgumentNullException(nameof(backgroundTasks));
            }

            foreach (var backgroundTask in backgroundTasks.ToList())
            {
                logger.LogDebug($"Enqueue background task type '{backgroundTask.GetType().Name}'");
                this.queue.Add(new Hst.Imager.Core.Models.BackgroundTasks.BackgroundTask
                {
                    Type = backgroundTask.GetType().Name,
                    Payload = JsonSerializer.Serialize(backgroundTask),
                    CancelAll = cancelAll
                });
            }

            return Task.CompletedTask;
        }

        public Task<IEnumerable<Hst.Imager.Core.Models.BackgroundTasks.BackgroundTask>> DequeueAsync()
        {
            logger.LogDebug("Dequeue background tasks");
            
            var backgroundTasks = new List<Hst.Imager.Core.Models.BackgroundTasks.BackgroundTask>();
            do
            {
                var backgroundTask = this.queue.Take();
                backgroundTasks.Add(backgroundTask);
            } while (this.queue.Count > 0);

            logger.LogDebug($"Dequeued background tasks '{(string.Join(",", backgroundTasks.Select(x => JsonSerializer.Serialize(x))))}'");
            
            return Task.FromResult(backgroundTasks.AsEnumerable());
        }

        public bool IsReady()
        {
            lock (LockObject)
            {
                return workerProcessId != 0;
            }
        }

        public void SetWorkerProcessId(int processId)
        {
            logger.LogDebug($"Set worker process id = {processId}");

            lock (LockObject)
            {
                this.workerProcessId = processId;
            }
        }
    }
}