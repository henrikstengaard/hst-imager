using System.Diagnostics;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Imager.GuiApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace Hst.Imager.GuiApp.Controllers;

[ApiController]
[Route("api/open-external")]
public class OpenExternalController : ControllerBase
{
    [HttpPost]
    public Task<IActionResult> Post(OpenExternalViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return Task.FromResult<IActionResult>(BadRequest(ModelState));
        }
        
        OpenUrl(model.Url);

        return Task.FromResult<IActionResult>(Ok());
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (OperatingSystem.IsWindows())
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}")
                {
                    CreateNoWindow = true
                });
            }
            else if (OperatingSystem.IsMacOs())
            {
                Process.Start("open", url);
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", url);
            }
            else
            {
                throw;
            }
        }
    }
}