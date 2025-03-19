using System.Linq;
using Hst.Imager.Core;
using Hst.Imager.Core.PhysicalDrives;
using Hst.Imager.GuiApp.BackgroundTasks;
using Hst.Imager.GuiApp.Extensions;
using Hst.Imager.GuiApp.Hubs;
using Hst.Imager.GuiApp.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Hst.Imager.GuiApp.Controllers
{
    using System.Threading.Tasks;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Microsoft.AspNetCore.Mvc;
    using Models.Requests;
    using Services;

    [ApiController]
    [Route("api/write")]
    public class WriteController(
        ILoggerFactory loggerFactory,
        IHubContext<ProgressHub> progressHubContext,
        AppState appState,
        IBackgroundTaskQueue backgroundTaskQueue,
        WorkerService workerService)
        : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Post(WriteRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var writeBackgroundTask = new WriteBackgroundTask
            {
                Title = request.Title,
                WritePhysicalDisk = request.WritePhysicalDisk,
                SourcePath = request.SourcePath,
                DestinationPath = request.DestinationPath,
                StartOffset = request.StartOffset,
                Size = request.Size,
                Byteswap = request.Byteswap
            };

            if (!workerService.IsRunning() && !request.WritePhysicalDisk)
            {
                var staticPhysicalDriveManager = new StaticPhysicalDriveManager([]);
                var handler = new WriteBackgroundTaskHandler(loggerFactory, staticPhysicalDriveManager,
                    appState);
                handler.ProgressUpdated += async (_, args) => await progressHubContext.SendProgress(args.Progress);

                await backgroundTaskQueue.QueueBackgroundWorkItemAsync(handler.Handle, writeBackgroundTask);

                return Ok();
            }

            await workerService.EnqueueAsync([writeBackgroundTask], true);

            return Ok();
        }
    }
}