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
using Hst.Imager.Core.PhysicalDrives;
using Hst.Imager.GuiApp.Extensions;

namespace Hst.Imager.GuiApp.Controllers
{
    [ApiController]
    [Route("api/format")]
    public class FormatController(
        ILoggerFactory loggerFactory,
        IHubContext<ProgressHub> progressHubContext,
        IHubContext<ResultHub> resultHubContext,
        IHubContext<ErrorHub> errorHubContext,
        IBackgroundTaskQueue backgroundTaskQueue,
        WorkerService workerService,
        AppState appState)
        : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Post(FormatRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var formatBackgroundTask = new FormatBackgroundTask
            {
                Title = request.Title,
                Path = request.Path,
                FormatType = request.FormatType,
                FileSystem = request.FileSystem,
                FileSystemPath = request.FileSystemPath,
                Size = request.Size,
                MaxPartitionSize = request.MaxPartitionSize,
                UseExperimental = request.UseExperimental,
                Byteswap = request.Byteswap
            };
            
            var infoBackgroundTask = new InfoBackgroundTask
            {
                Path = request.Path,
                Byteswap = request.Byteswap
            };

            var hasPhysicalDrivePaths = PhysicalDriveHelper.HasPhysicalDrivePaths(request.Path);
            
            if (!workerService.IsRunning() && !hasPhysicalDrivePaths)
            {
                var staticPhysicalDriveManager = new StaticPhysicalDriveManager([]);
                var formatHandler = new FormatBackgroundTaskHandler(loggerFactory, staticPhysicalDriveManager, appState);
                formatHandler.ProgressUpdated += async (_, args) => await progressHubContext.SendProgress(args.Progress);
                
                await backgroundTaskQueue.QueueBackgroundWorkItemAsync(formatHandler.Handle, formatBackgroundTask);

                var infoHandler = new InfoBackgroundTaskHandler(loggerFactory, staticPhysicalDriveManager,
                    appState);
                infoHandler.MediaInfoRead += async (_, args) => await resultHubContext.SendInfoResult(
                    args.MediaInfo?.ToViewModel());;
                infoHandler.ErrorOccurred += async (_, args) => await errorHubContext.SendError(args.Message);

                await backgroundTaskQueue.QueueBackgroundWorkItemAsync(infoHandler.Handle, infoBackgroundTask);

                return Ok();
            }

            await workerService.EnqueueAsync([formatBackgroundTask], true);

            await workerService.EnqueueAsync([infoBackgroundTask]);

            return Ok();
        }
    }
}