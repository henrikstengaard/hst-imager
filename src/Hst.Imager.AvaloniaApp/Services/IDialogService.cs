using System.Collections.Generic;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.ViewModels;

namespace Hst.Imager.AvaloniaApp.Services;

public class FileFilterItem
{
    public string Name { get; set; } = string.Empty;
    public IEnumerable<string> Extensions { get; set; } = [];
}

public interface IDialogService
{
    Task<string?> ShowOpenFileDialogAsync(string title, IEnumerable<FileFilterItem> filters);
    Task<string?> ShowSaveFileDialogAsync(string title, IEnumerable<FileFilterItem> filters, string? defaultFileName = null);
    Task<string?> ShowOpenFolderDialogAsync(string title);
    Task<bool> ShowConfirmDialogAsync(string title, string description);

    /// <summary>
    /// Show initialize partition table dialog. Returns true, if initialize is clicked.
    /// </summary>
    Task<bool> ShowInitializePartitionTableDialogAsync(InitializePartitionTableViewModel viewModel);
    Task OpenExternalAsync(string url);
}
