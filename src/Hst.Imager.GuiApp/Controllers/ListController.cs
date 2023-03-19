namespace Hst.Imager.GuiApp.Controllers
{
    using System.Threading.Tasks;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Microsoft.AspNetCore.Mvc;
    using Services;

    [ApiController]
    [Route("api/list")]
    public class ListController : ControllerBase
    {
        private readonly WorkerService workerService;

        public ListController(WorkerService workerService)
        {
            this.workerService = workerService;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            await workerService.EnqueueAsync(new[] { new ListBackgroundTask() });

            return Ok();
        }
    }
}