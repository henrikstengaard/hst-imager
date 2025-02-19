namespace Hst.Imager.GuiApp.Controllers
{
    using System.Threading.Tasks;
    using ElectronNET.API;
    using Hst.Imager.Core.Helpers;
    using Hst.Imager.GuiApp.Models;
    using Microsoft.AspNetCore.Mvc;
    using Models.Requests;

    [ApiController]
    [Route("api/license")]
    public class LicenseController : ControllerBase
    {
        private readonly AppState appState;

        public LicenseController(AppState appState)
        {
            this.appState = appState;
        }

        [HttpPost]
        public async Task<IActionResult> Post(LicenseRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!request.LicenseAgreed)
            {
                Electron.App.Exit();
            }
            
            await ApplicationDataHelper.AgreeLicense(GetType().Assembly, appState.AppDataPath, request.LicenseAgreed);
            
            return Ok();
        }
    }
}