namespace HstWbInstaller.Imager.GuiApp.Controllers
{
    using System.Threading.Tasks;
    using BackgroundTasks;
    using Core.Models.BackgroundTasks;
    using Hubs;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;
    using Models;
    using Models.Requests;
    using Services;

    [ApiController]
    [Route("api/blank")]
    public class BlankController : ControllerBase
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly IHubContext<ProgressHub> progressHubContext;
        private readonly IBackgroundTaskQueue backgroundTaskQueue;
        private readonly AppState appState;

        public BlankController(ILoggerFactory loggerFactory, IHubContext<ProgressHub> progressHubContext,
            IBackgroundTaskQueue backgroundTaskQueue, AppState appState)
        {
            this.loggerFactory = loggerFactory;
            this.progressHubContext = progressHubContext;
            this.backgroundTaskQueue = backgroundTaskQueue;
            this.appState = appState;
        }

        [HttpPost]
        public async Task<IActionResult> Post(BlankRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var task = new BlankBackgroundTask
            {
                Title = request.Title,
                Path = request.Path,
                Size = request.Size,
                CompatibleSize = request.CompatibleSize
            };
            var handler = new BlankBackgroundTaskHandler(loggerFactory, progressHubContext, appState);
            await backgroundTaskQueue.QueueBackgroundWorkItemAsync(handler.Handle, task);

            return Ok();
        }
    }
}