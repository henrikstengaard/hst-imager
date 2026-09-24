using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Hst.Imager.AvaloniaApp.Services;

public class DialogService : IDialogService
{
    private Window? _window;

    public void SetWindow(Window window)
    {
        _window = window;
    }

    public async Task<string?> ShowOpenFileDialogAsync(string title, IEnumerable<FileFilterItem> filters)
    {
        if (_window == null) return null;

        var fileTypeFilters = filters.Select(f => new FilePickerFileType(f.Name)
        {
            Patterns = f.Extensions.Select(e => e.StartsWith('*') ? e : $"*.{e}").ToList()
        }).ToList();

        var result = await _window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypeFilters
        });

        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }

    public async Task<string?> ShowSaveFileDialogAsync(string title, IEnumerable<FileFilterItem> filters, string? defaultFileName = null)
    {
        if (_window == null) return null;

        var fileTypeFilters = filters.Select(f => new FilePickerFileType(f.Name)
        {
            Patterns = f.Extensions.Select(e => e.StartsWith('*') ? e : $"*.{e}").ToList()
        }).ToList();

        var result = await _window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            FileTypeChoices = fileTypeFilters,
            SuggestedFileName = defaultFileName
        });

        return result?.Path.LocalPath;
    }

    public async Task<string?> ShowOpenFolderDialogAsync(string title)
    {
        if (_window == null) return null;

        var result = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return result.Count > 0 ? result[0].Path.LocalPath : null;
    }
}
