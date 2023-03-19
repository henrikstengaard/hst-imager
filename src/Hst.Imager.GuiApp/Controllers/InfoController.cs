namespace Hst.Imager.GuiApp.Controllers
{
    using System.Threading.Tasks;
    using BackgroundTasks;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Hubs;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;
    using Models;
    using Models.Requests;
    using Services;

    [ApiController]
    [Route("api/info")]
    public class InfoController : ControllerBase
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly IHubContext<ResultHub> resultHubContext;
        private readonly IHubContext<ErrorHub> errorHubContext;
        private readonly IBackgroundTaskQueue backgroundTaskQueue;
        private readonly WorkerService workerService;
        private readonly AppState appState;

        public InfoController(ILoggerFactory loggerFactory, IHubContext<ResultHub> resultHubContext,
            IHubContext<ErrorHub> errorHubContext, IBackgroundTaskQueue backgroundTaskQueue,
            WorkerService workerService, AppState appState)
        {
            this.loggerFactory = loggerFactory;
            this.resultHubContext = resultHubContext;
            this.errorHubContext = errorHubContext;
            this.backgroundTaskQueue = backgroundTaskQueue;
            this.workerService = workerService;
            this.appState = appState;
        }

        [HttpPost]
        public async Task<IActionResult> Post(InfoRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!this.workerService.IsRunning() && request.SourceType == InfoRequest.SourceTypeEnum.ImageFile)
            {
                var task = new ImageFileInfoBackgroundTask
                {
                    Path = request.Path
                };
                var handler =
                    new ImageFileInfoBackgroundTaskHandler(loggerFactory, resultHubContext, errorHubContext, appState);
                await backgroundTaskQueue.QueueBackgroundWorkItemAsync(handler.Handle, task);

                return Ok();
            }

            await workerService.EnqueueAsync(new[]
            {
                new InfoBackgroundTask
                {
                    Path = request.Path
                }
            });

            return Ok();
        }
    }
}