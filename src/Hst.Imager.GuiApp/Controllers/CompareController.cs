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
    [Route("api/compare")]
    public class CompareController : ControllerBase
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly IHubContext<ProgressHub> progressHubContext;
        private readonly IBackgroundTaskQueue backgroundTaskQueue;
        private readonly WorkerService workerService;
        private readonly AppState appState;

        public CompareController(ILoggerFactory loggerFactory, IHubContext<ProgressHub> progressHubContext,
            IBackgroundTaskQueue backgroundTaskQueue, WorkerService workerService, AppState appState)
        {
            this.loggerFactory = loggerFactory;
            this.progressHubContext = progressHubContext;
            this.backgroundTaskQueue = backgroundTaskQueue;
            this.workerService = workerService;
            this.appState = appState;
        }

        [HttpPost]
        public async Task<IActionResult> Post(CompareRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (request.SourceType == CompareRequest.SourceTypeEnum.ImageFile)
            {
                var task = new ImageFileCompareBackgroundTask
                {
                    Title = request.Title,
                    SourcePath = request.SourcePath,
                    DestinationPath = request.DestinationPath
                };
                var handler =
                    new ImageFileCompareBackgroundTaskHandler(loggerFactory, progressHubContext, appState);
                await backgroundTaskQueue.QueueBackgroundWorkItemAsync(handler.Handle, task);

                return Ok();
            }

            await workerService.EnqueueAsync(new[]
            {
                new CompareBackgroundTask
                {
                    Title = request.Title,
                    SourcePath = request.SourcePath,
                    DestinationPath = request.DestinationPath
                }
            });

            return Ok();
        }
    }
}