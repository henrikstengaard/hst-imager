namespace Hst.Imager.GuiApp.Controllers
{
    using System.Threading.Tasks;
    using Core.Models;
    using ElectronNET.API;
    using Hst.Imager.Core.Helpers;
    using Microsoft.AspNetCore.Mvc;
    using Models.Requests;

    [ApiController]
    [Route("api/license")]
    public class LicenseController : ControllerBase
    {
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
            
            await ApplicationDataHelper.AgreeLicense(GetType().Assembly, Constants.AppName, request.LicenseAgreed);
            
            return Ok();
        }
    }
}