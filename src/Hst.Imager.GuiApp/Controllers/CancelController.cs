namespace Hst.Imager.GuiApp.Controllers
{
    using System.Threading.Tasks;
    using Extensions;
    using Hubs;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.SignalR;

    /// <summary>
    /// cancel controller to cancel active background task list
    /// </summary>
    [ApiController]
    [Route("api/cancel")]
    public class CancelController : ControllerBase
    {
        private readonly IHubContext<WorkerHub> workerHubContext;

        public CancelController(IHubContext<WorkerHub> workerHubContext)
        {
            this.workerHubContext = workerHubContext;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            await workerHubContext.CancelBackgroundTask();
            return Ok();
        }
    }
}