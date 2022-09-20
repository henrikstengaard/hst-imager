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
    public class OptimizeController : ControllerBase
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly IHubContext<ProgressHub> progressHubContext;
        private readonly IBackgroundTaskQueue backgroundTaskQueue;
        private readonly AppState appState;

        public OptimizeController(ILoggerFactory loggerFactory, IHubContext<ProgressHub> progressHubContext,
            IBackgroundTaskQueue backgroundTaskQueue, AppState appState)
        {
            this.loggerFactory = loggerFactory;
            this.progressHubContext = progressHubContext;
            this.backgroundTaskQueue = backgroundTaskQueue;
            this.appState = appState;
        }
        
        [HttpPost]
        public async Task<IActionResult> Post(OptimizeRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            var task = new OptimizeBackgroundTask
            {
                Title = request.Title,
                Path = request.Path
            };
            var handler = new OptimizeBackgroundTaskHandler(loggerFactory, progressHubContext, appState);
            await backgroundTaskQueue.QueueBackgroundWorkItemAsync(handler.Handle, task);
            
            return Ok();            
        }
    }
}