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
    [Route("api/info")]
    public class InfoController(
        ILoggerFactory loggerFactory,
        IHubContext<ResultHub> resultHubContext,
        IHubContext<ErrorHub> errorHubContext,
        IBackgroundTaskQueue backgroundTaskQueue,
        WorkerService workerService,
        AppState appState)
        : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Post(InfoRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var infoBackgroundTask = new InfoBackgroundTask
            {
                Path = request.Path,
                Byteswap = request.Byteswap,
                AllowNonExisting = request.AllowNonExisting
            };

            var hasPhysicalDrivePaths = PhysicalDriveHelper.HasPhysicalDrivePaths(request.Path);

            if (!workerService.IsRunning() && !hasPhysicalDrivePaths)
            {
                var staticPhysicalDriveManager = new StaticPhysicalDriveManager([]);
                var handler = new InfoBackgroundTaskHandler(loggerFactory, staticPhysicalDriveManager, appState);
                handler.MediaInfoRead += async (_, args) => await resultHubContext.SendInfoResult(
                    args.MediaInfo?.ToViewModel());
                handler.ErrorOccurred += async (_, args) => await errorHubContext.SendError(args.Message);

                await backgroundTaskQueue.QueueBackgroundWorkItemAsync(handler.Handle, infoBackgroundTask);

                return Ok();
            }

            await workerService.EnqueueAsync([infoBackgroundTask]);

            return Ok();
        }
    }
}