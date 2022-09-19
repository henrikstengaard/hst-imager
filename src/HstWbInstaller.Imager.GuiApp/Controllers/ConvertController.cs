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
    [Route("api/convert")]
    public class ConvertController : ControllerBase
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly IHubContext<ProgressHub> progressHubContext;
        private readonly IBackgroundTaskQueue backgroundTaskQueue;
        private readonly AppState appState;

        public ConvertController(ILoggerFactory loggerFactory, IHubContext<ProgressHub> progressHubContext,
            IBackgroundTaskQueue backgroundTaskQueue, AppState appState)
        {
            this.loggerFactory = loggerFactory;
            this.progressHubContext = progressHubContext;
            this.backgroundTaskQueue = backgroundTaskQueue;
            this.appState = appState;
        }
        
        [HttpPost]
        public async Task<IActionResult> Post(ConvertRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var task = new ConvertBackgroundTask
            {
                Title = request.Title,
                SourcePath = request.SourcePath,
                DestinationPath = request.DestinationPath
            };
            var handler = new ConvertBackgroundTaskHandler(loggerFactory, progressHubContext, appState);
            await backgroundTaskQueue.QueueBackgroundWorkItemAsync(handler.Handle, task);
            
            return Ok();
        }
    }
}