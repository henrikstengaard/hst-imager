using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Models;
using Hst.Imager.AvaloniaApp.Services;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using Serilog;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly string[] _startupArgs;
    private ViewModelBase _currentPage;
    private bool _isSidebarExpanded;

    public MainWindowViewModel(IServiceProvider services, string[] startupArgs)
    {
        _services = services;
        _startupArgs = startupArgs;
        _currentPage = services.GetRequiredService<StartViewModel>();
        Progress = services.GetRequiredService<ProgressViewModel>();
        _isSidebarExpanded = services.GetRequiredService<AppStateModel>().Settings.SidebarExpanded;

        services.GetRequiredService<INavigationService>().NavigationRequested += NavigateTo;

        NavigateToCommand = ReactiveCommand.Create<string>(NavigateTo);
        ToggleSidebarCommand = ReactiveCommand.CreateFromTask(ToggleSidebarAsync);
        ElevateCommand = ReactiveCommand.CreateFromTask(ElevateAsync);
    }

    public ProgressViewModel Progress { get; }

    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    public bool IsElevated { get; } = User.IsAdministrator();

    public IReadOnlyList<NavItemViewModel> NavItems { get; } =
    [
        new("Start", "Start", "fa-home") { IsActive = true },
        new("Read", "Read", "fa-file-import"),
        new("Write", "Write", "fa-file-export"),
        new("Info", "Info", "fa-info"),
        new("Transfer", "Transfer", "fa-exchange-alt"),
        new("Compare", "Compare", "fa-check"),
        new("Blank", "Blank", "fa-plus"),
        new("Optimize", "Optimize", "fa-compress"),
        new("Format", "Format", "fa-eraser")
    ];

    public IReadOnlyList<NavItemViewModel> FooterNavItems { get; } =
    [
        new("Settings", "Settings", "fa-cog"),
        new("About", "About", "fa-question")
    ];

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        set => this.RaiseAndSetIfChanged(ref _isSidebarExpanded, value);
    }

    public ReactiveCommand<string, Unit> NavigateToCommand { get; }
    public ReactiveCommand<Unit, Unit> ElevateCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleSidebarCommand { get; }

    public void NavigateTo(string page)
    {
        CurrentPage = page switch
        {
            "Start" => _services.GetRequiredService<StartViewModel>(),
            "Read" => _services.GetRequiredService<ReadViewModel>(),
            "Write" => _services.GetRequiredService<WriteViewModel>(),
            "Info" => _services.GetRequiredService<InfoViewModel>(),
            "Transfer" => _services.GetRequiredService<TransferViewModel>(),
            "Compare" => _services.GetRequiredService<CompareViewModel>(),
            "Blank" => _services.GetRequiredService<BlankViewModel>(),
            "Optimize" => _services.GetRequiredService<OptimizeViewModel>(),
            "Format" => _services.GetRequiredService<FormatViewModel>(),
            "Settings" => _services.GetRequiredService<SettingsViewModel>(),
            "About" => _services.GetRequiredService<AboutViewModel>(),
            _ => _services.GetRequiredService<StartViewModel>()
        };

        foreach (var navItem in NavItems.Concat(FooterNavItems))
        {
            navItem.IsActive = navItem.Page == page;
        }
    }

    private async Task ToggleSidebarAsync()
    {
        IsSidebarExpanded = !IsSidebarExpanded;

        var settings = _services.GetRequiredService<AppStateModel>().Settings;
        settings.SidebarExpanded = IsSidebarExpanded;
        try
        {
            await _services.GetRequiredService<ISettingsService>().SaveSettingsAsync(settings);
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to save sidebar expanded setting");
        }
    }

    private async Task ElevateAsync()
    {
        // exits current app, if elevated app is started
        await User.ElevateAsync(_startupArgs, _services.GetRequiredService<Models.AppStateModel>().Settings);
    }
}
