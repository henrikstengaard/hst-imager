using System.Collections.Generic;
using System.Reactive;
using Hst.Imager.AvaloniaApp.Services;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class StartAction
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Page { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

public class StartViewModel : ViewModelBase
{
    public StartViewModel(INavigationService navigationService)
    {
        NavigateCommand = ReactiveCommand.Create<string>(navigationService.NavigateTo);
    }

    public ReactiveCommand<string, Unit> NavigateCommand { get; }

    public List<StartAction> Actions { get; } =
    [
        new() { Title = "Read", Description = "Read a physical disk or part of to an image file.", Page = "Read", Icon = "fa-upload" },
        new() { Title = "Write", Description = "Write an image file or part of to a physical disk.", Page = "Write", Icon = "fa-download" },
        new() { Title = "Info", Description = "Display information about an image file or a physical disk.", Page = "Info", Icon = "fa-info" },
        new() { Title = "Transfer", Description = "Transfer converts, imports or exports from an image file or part of to another.", Page = "Transfer", Icon = "fa-exchange-alt" },
        new() { Title = "Compare", Description = "Compare image files and physical disks byte by byte.", Page = "Compare", Icon = "fa-check" },
        new() { Title = "Blank", Description = "Create a blank image file.", Page = "Blank", Icon = "fa-plus" },
        new() { Title = "Optimize", Description = "Optimize an image file size.", Page = "Optimize", Icon = "fa-compress" },
        new() { Title = "Format", Description = "Format a physical disk or an image file.", Page = "Format", Icon = "fa-eraser" }
    ];
}
