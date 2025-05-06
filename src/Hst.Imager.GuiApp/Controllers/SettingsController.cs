namespace Hst.Imager.GuiApp.Controllers
{
    using System.Threading.Tasks;
    using Hst.Imager.Core.Helpers;
    using Hst.Imager.Core.Models;
    using Microsoft.AspNetCore.Mvc;
    using Models;

    [ApiController]
    [Route("api/settings")]
    public class SettingsController : ControllerBase
    {
        private readonly AppState appState;

        public SettingsController(AppState appState)
        {
            this.appState = appState;
        }

        [HttpPost]
        public async Task<IActionResult> Post(Settings request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            appState.Settings = request;
            await ApplicationDataHelper.WriteSettings(appState.AppDataPath, Constants.AppName, request);

            return Ok();
        }
    }
}