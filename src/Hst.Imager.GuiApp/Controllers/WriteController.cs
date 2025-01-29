namespace Hst.Imager.GuiApp.Controllers
{
    using System.Threading.Tasks;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Microsoft.AspNetCore.Mvc;
    using Models.Requests;
    using Services;

    [ApiController]
    [Route("api/write")]
    public class WriteController : ControllerBase
    {
        private readonly WorkerService workerService;

        public WriteController(WorkerService workerService)
        {
            this.workerService = workerService;
        }

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
                SourcePath = request.SourcePath,
                DestinationPath = request.DestinationPath,
                Size = request.Size,
                Verify = request.Verify,
                Force = request.Force,
                Retries = request.Retries,
                Byteswap = request.Byteswap
            };

            await workerService.EnqueueAsync([writeBackgroundTask], true);

            return Ok();
        }
    }
}