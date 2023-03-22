namespace Hst.Imager.GuiApp.Controllers
{
    using System.Threading.Tasks;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Microsoft.AspNetCore.Mvc;
    using Models.Requests;
    using Services;

    [ApiController]
    [Route("api/read")]
    public class ReadController : ControllerBase
    {
        private readonly WorkerService workerService;

        public ReadController(WorkerService workerService)
        {
            this.workerService = workerService;
        }

        [HttpPost]
        public async Task<IActionResult> Post(ReadRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var readBackgroundTask = new ReadBackgroundTask
            {
                Title = request.Title,
                SourcePath = request.SourcePath,
                DestinationPath = request.DestinationPath,
                Size = request.Size,
                Verify = request.Verify,
                Force = request.Force,
                Retries = request.Retries
            };

            await workerService.EnqueueAsync(new[] { readBackgroundTask });

            return Ok();
        }
    }
}