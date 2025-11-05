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
    [Route("api/transfer")]
    public class TransferController(
        ILoggerFactory loggerFactory,
        IHubContext<ProgressHub> progressHubContext,
        IBackgroundTaskQueue backgroundTaskQueue,
        AppState appState)
        : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Post(TransferRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var task = new TransferBackgroundTask
            {
                Title = request.Title,
                SourcePath = request.SourcePath,
                DestinationPath = request.DestinationPath,
                SrcStartOffset = request.SrcStartOffset,
                DestStartOffset = request.DestStartOffset,
                Size = request.Size,
                Byteswap = request.Byteswap
            };
            
            var handler = new TransferBackgroundTaskHandler(loggerFactory, appState);
            handler.ProgressUpdated += async (_, args) => await progressHubContext.SendProgress(args.Progress);

            await backgroundTaskQueue.QueueBackgroundWorkItemAsync(handler.Handle, task);
            
            return Ok();
        }
    }
}