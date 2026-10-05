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
    /// <summary>
    /// Show partition dialog with details of selected or added partition in partition view model. Returns true, if OK or Add is
    /// clicked and false, if dialog is cancelled.
    /// </summary>
    Task<bool> ShowPartitionDialogAsync(PartitionViewModel viewModel);
    /// <summary>
    /// Show file systems dialog to edit file systems in a rigid disk block. Returns true, if OK is clicked.
    /// </summary>
    Task<bool> ShowRdbFileSystemsDialogAsync(RdbFileSystemsViewModel viewModel);
    /// <summary>
    /// Show media selection dialog to select source or destination media. Returns true, if OK is clicked.
    /// </summary>
    Task<bool> ShowMediaSelectionDialogAsync(MediaSelectionViewModel viewModel);
    Task OpenExternalAsync(string url);
}
