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
    [Route("api/compare")]
    public class CompareController(
        ILoggerFactory loggerFactory,
        IHubContext<ProgressHub> progressHubContext,
        IBackgroundTaskQueue backgroundTaskQueue,
        WorkerService workerService,
        AppState appState)
        : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Post(CompareRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var compareBackgroundTask = new CompareBackgroundTask
            {
                Title = request.Title,
                SourcePath = request.SourcePath,
                SourceStartOffset = request.SourceStartOffset,
                DestinationPath = request.DestinationPath,
                DestinationStartOffset = request.DestinationStartOffset,
                Byteswap = request.Byteswap
            };
            
            var hasPhysicalDrivePaths = PhysicalDriveHelper.HasPhysicalDrivePaths(request.SourcePath,
                request.DestinationPath);

            if (!workerService.IsRunning() && !hasPhysicalDrivePaths)
            {
                var staticPhysicalDriveManager = new StaticPhysicalDriveManager([]);
                var handler =
                    new CompareBackgroundTaskHandler(loggerFactory, staticPhysicalDriveManager, appState);
                handler.ProgressUpdated += async (_, args) => await progressHubContext.SendProgress(args.Progress);

                await backgroundTaskQueue.QueueBackgroundWorkItemAsync(handler.Handle, compareBackgroundTask);
                return Ok();
            }

            await workerService.EnqueueAsync([compareBackgroundTask], true);

            return Ok();
        }
    }
}