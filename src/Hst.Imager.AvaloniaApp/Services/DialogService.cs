using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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

    public async Task<bool> ShowConfirmDialogAsync(string title, string description)
    {
        if (_window == null) return false;

        var cancelButton = new Button { Content = "Cancel", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        var okButton = new Button { Content = "OK", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        okButton.Classes.Add("accent");

        var dialog = new Window
        {
            Title = title,
            Width = 520,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancelButton, okButton }
                    }
                }
            }
        };

        cancelButton.Click += (_, _) => dialog.Close(false);
        okButton.Click += (_, _) => dialog.Close(true);

        return await dialog.ShowDialog<bool>(_window);
    }

    public async Task OpenExternalAsync(string url)
    {
        if (_window == null || string.IsNullOrWhiteSpace(url)) return;

        var uri = Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri
            : new Uri(System.IO.Path.GetFullPath(url));

        await _window.Launcher.LaunchUriAsync(uri);
    }
}
