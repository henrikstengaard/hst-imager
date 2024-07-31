using Hst.Imager.GuiApp.Dialogs;

namespace Hst.Imager.GuiApp.Controllers
{
    using System.Linq;
    using System.Threading.Tasks;
    using ElectronNET.API;
    using Hst.Imager.Core.Models.BackgroundTasks;
    using Hubs;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.SignalR;
    using Models;
    using Services;

    [ApiController]
    [Route("api/show-open-dialog")]
    public class ShowOpenDialogController : ControllerBase
    {
        private readonly IBackgroundTaskQueue backgroundTaskQueue;
        private readonly IHubContext<ShowDialogResultHub> showDialogResultContext;

        public ShowOpenDialogController(IBackgroundTaskQueue backgroundTaskQueue,
            IHubContext<ShowDialogResultHub> showDialogResultContext)
        {
            this.backgroundTaskQueue = backgroundTaskQueue;
            this.showDialogResultContext = showDialogResultContext;
        }

        [HttpPost]
        public async Task<IActionResult> Post(DialogViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await backgroundTaskQueue.QueueBackgroundWorkItemAsync(ShowOpenDialogWorkItem, new ShowDialogBackgroundTask
            {
                Id = model.Id,
                Title = model.Title,
                Path = model.Path,
                FileFilters = model.FileFilters.Select(x => new FileFilter
                {
                    Name = x.Name,
                    Extensions = x.Extensions
                })
            });

            return Ok();
        }

        private async ValueTask ShowOpenDialogWorkItem(IBackgroundTaskContext context)
        {
            if (context.BackgroundTask is not ShowDialogBackgroundTask showDialogBackgroundTask)
            {
                return;
            }

            var path = HybridSupport.IsElectronActive
                ? await ElectronDialog.ShowOpenDialog(showDialogBackgroundTask.Title, showDialogBackgroundTask.FileFilters, showDialogBackgroundTask.Path)
                : OperatingSystemDialog.ShowOpenDialog(showDialogBackgroundTask.Title, showDialogBackgroundTask.FileFilters, showDialogBackgroundTask.Path);

            await showDialogResultContext.Clients.All.SendAsync("ShowDialogResult", new ShowDialogResult
            {
                Id = showDialogBackgroundTask.Id,
                IsSuccess = path != null,
                Paths = new []{ path }
            }, context.Token);
        }
    }
}