namespace Hst.Imager.GuiApp.Controllers
{
    using System.Threading.Tasks;
    using Hst.Imager.Core.Helpers;
    using Hst.Imager.Core.Models;
    using Microsoft.AspNetCore.Mvc;
    using Models;

    /// <summary>
    /// get app state
    /// </summary>
    [ApiController]
    [Route("api/app-state")]
    public class AppStateController : ControllerBase
    {
        private readonly AppState appState;

        public AppStateController(AppState appState)
        {
            this.appState = appState;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            appState.Settings = await ApplicationDataHelper.ReadSettings<Settings>(Constants.AppName);

            return Ok(appState);
        }
    }
}