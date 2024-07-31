using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ElectronNET.API;
using ElectronNET.API.Entities;
using FileFilter = Hst.Imager.Core.Models.BackgroundTasks.FileFilter;

namespace Hst.Imager.GuiApp.Dialogs;

public static class ElectronDialog
{
    public static async Task<string> ShowOpenDialog(string title, IEnumerable<FileFilter> fileFilters, string path)
    {
        var browserWindow = Electron.WindowManager.BrowserWindows.FirstOrDefault();
        if (browserWindow == null)
        {
            return null;
        }

        var paths = await Electron.Dialog.ShowOpenDialogAsync(browserWindow, new OpenDialogOptions
        {
            Title = title,
            Filters = fileFilters.Select(x => new ElectronNET.API.Entities.FileFilter
            {
                Name = x.Name,
                Extensions = x.Extensions.ToArray()
            }).ToArray(),
            DefaultPath = path
        });

        return paths.FirstOrDefault();
    }

    public static async Task<string> ShowSaveDialog(string title, IEnumerable<FileFilter> fileFilters, string path)
    {
        var browserWindow = Electron.WindowManager.BrowserWindows.FirstOrDefault();
        if (browserWindow == null)
        {
            return null;
        }

        return await Electron.Dialog.ShowSaveDialogAsync(browserWindow, new SaveDialogOptions
        {
            Title = title,
            Filters = fileFilters.Select(x => new ElectronNET.API.Entities.FileFilter
            {
                Name = x.Name,
                Extensions = x.Extensions.ToArray()
            }).ToArray(),
            DefaultPath = path
        });
    }
}