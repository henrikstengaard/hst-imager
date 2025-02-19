using Hst.Imager.Core.Models.BackgroundTasks;
using Hst.Imager.GuiApp.BackgroundTasks;
using Hst.Imager.GuiApp.Hubs;
using Hst.Imager.GuiApp.Models;
using Hst.Imager.GuiApp.Models.Requests;
using Hst.Imager.GuiApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Hst.Imager.GuiApp.Controllers
{
    [ApiController]
    [Route("api/format")]
    public class FormatController : ControllerBase
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly IHubContext<ProgressHub> progressHubContext;
        private readonly IHubContext<ResultHub> resultHubContext;
        private readonly IHubContext<ErrorHub> errorHubContext;
        private readonly IBackgroundTaskQueue backgroundTaskQueue;
        private readonly WorkerService workerService;
        private readonly AppState appState;

        public FormatController(ILoggerFactory loggerFactory, IHubContext<ProgressHub> progressHubContext,
            IHubContext<ResultHub> resultHubContext,IHubContext<ErrorHub> errorHubContext,
            IBackgroundTaskQueue backgroundTaskQueue, WorkerService workerService, AppState appState)
        {
            this.loggerFactory = loggerFactory;
            this.progressHubContext = progressHubContext;
            this.resultHubContext = resultHubContext;
            this.errorHubContext = errorHubContext;
            this.backgroundTaskQueue = backgroundTaskQueue;
            this.workerService = workerService;
            this.appState = appState;
        }

        [HttpPost]
        public async Task<IActionResult> Post(FormatRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!workerService.IsRunning() && request.SourceType == FormatRequest.SourceTypeEnum.ImageFile)
            {
                var formatTask = new ImageFileFormatBackgroundTask
                {
                    Title = request.Title,
                    Path = request.Path,
                    FormatType = request.FormatType,
                    FileSystem = request.FileSystem,
                    FileSystemPath = request.FileSystemPath,
                    Size = request.Size,
                    Byteswap = request.Byteswap
                };
                var formatHandler =
                    new ImageFileFormatBackgroundTaskHandler(loggerFactory, progressHubContext, appState);
                await backgroundTaskQueue.QueueBackgroundWorkItemAsync(formatHandler.Handle, formatTask);

                var infoTask = new ImageFileInfoBackgroundTask
                {
                    Path = request.Path,
                    Byteswap = request.Byteswap
                };
                var infoHandler =
                    new ImageFileInfoBackgroundTaskHandler(loggerFactory, resultHubContext, errorHubContext,
                    appState);
                await backgroundTaskQueue.QueueBackgroundWorkItemAsync(infoHandler.Handle, infoTask);

                return Ok();
            }

            await workerService.EnqueueAsync(
            [
                new FormatBackgroundTask
                {
                    Title = request.Title,
                    Path = request.Path,
                    FormatType = request.FormatType,
                    FileSystem = request.FileSystem,
                    FileSystemPath = request.FileSystemPath,
                    Size = request.Size,
                    Byteswap = request.Byteswap
                }
            ], true);

            await workerService.EnqueueAsync(
            [
                new InfoBackgroundTask
                {
                    Path = request.Path,
                    Byteswap = request.Byteswap
                }
            ]);

            return Ok();
        }
    }
}
