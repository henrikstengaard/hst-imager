using System.Collections.Generic;
using System.Linq;
using Hst.Core;
using FileFilter = Hst.Imager.Core.Models.BackgroundTasks.FileFilter;

namespace Hst.Imager.GuiApp.Dialogs;

public static class OperatingSystemDialog
{
    public static string ShowOpenDialog(string title, IEnumerable<FileFilter> fileFilters, string initialDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return ShowOpenDialogWindows(title, fileFilters, initialDirectory);
        }

        return null;
    }

    public static string ShowSaveDialog(string title, IEnumerable<FileFilter> fileFilters, string initialDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return ShowSaveDialogWindows(title, fileFilters, initialDirectory);
        }

        return null;
    }

    private static string ShowOpenDialogWindows(string title, IEnumerable<FileFilter> fileFilters,
        string initialDirectory) =>
        OpenFileDialog.OpenFile(out var path,
            title,
            FormatFileFilters(fileFilters),
            initialDirectory)
            ? path
            : null;

    private static string ShowSaveDialogWindows(string title, IEnumerable<FileFilter> fileFilters,
        string initialDirectory) =>
        SaveFileDialog.SaveFile(out var path,
            title,
            FormatFileFilters(fileFilters),
            initialDirectory)
            ? path
            : null;

    private static string FormatFileFilters(IEnumerable<FileFilter> fileFilters) =>
        string.Join("|", fileFilters.Select(FormatFileFilter));

    private static string FormatFileFilter(FileFilter fileFilter)
    {
        var extensions = fileFilter.Extensions.Select(extension => $"*.{extension}").ToArray();
        return $"{fileFilter.Name} ({string.Join(",", extensions)})|{string.Join(";", extensions)}";
    }
}