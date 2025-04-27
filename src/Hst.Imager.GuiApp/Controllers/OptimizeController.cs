using Hst.Imager.Core.PhysicalDrives;
using Hst.Imager.GuiApp.Extensions;

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
    [Route("api/optimize")]
    public class OptimizeController(
        ILoggerFactory loggerFactory,
        IHubContext<ProgressHub> progressHubContext,
        IHubContext<ResultHub> resultHubContext,
        IHubContext<ErrorHub> errorHubContext,
        IBackgroundTaskQueue backgroundTaskQueue,
        AppState appState)
        : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Post(OptimizeRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            var optimizeTask = new OptimizeBackgroundTask
            {
                Title = request.Title,
                Path = request.Path,
                Size = request.Size
            };
            var optimizeHandler = new OptimizeBackgroundTaskHandler(loggerFactory, progressHubContext, appState);

            await backgroundTaskQueue.QueueBackgroundWorkItemAsync(optimizeHandler.Handle, optimizeTask);
            
            var infoBackgroundTask = new InfoBackgroundTask
            {
                Path = request.Path,
                Byteswap = request.Byteswap
            };

            var staticPhysicalDriveManager = new StaticPhysicalDriveManager([]);
            var infoHandler = new InfoBackgroundTaskHandler(loggerFactory, staticPhysicalDriveManager,
                appState);
            infoHandler.MediaInfoRead += async (_, args) => await resultHubContext.SendInfoResult(
                args.MediaInfo?.ToViewModel());;
            infoHandler.ErrorOccurred += async (_, args) => await errorHubContext.SendError(args.Message);

            await backgroundTaskQueue.QueueBackgroundWorkItemAsync(infoHandler.Handle, infoBackgroundTask);
            
            return Ok();            
        }
    }
}