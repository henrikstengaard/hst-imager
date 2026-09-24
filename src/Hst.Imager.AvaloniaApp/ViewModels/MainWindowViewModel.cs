using System;
using System.Reactive;
using Hst.Imager.AvaloniaApp.Services;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private ViewModelBase _currentPage;
    private string _currentPageName = "Start";

    public MainWindowViewModel(IServiceProvider services)
    {
        _services = services;
        _currentPage = services.GetRequiredService<StartViewModel>();

        NavigateToCommand = ReactiveCommand.Create<string>(NavigateTo);
    }

    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    public string CurrentPageName
    {
        get => _currentPageName;
        private set => this.RaiseAndSetIfChanged(ref _currentPageName, value);
    }

    public ReactiveCommand<string, Unit> NavigateToCommand { get; }

    public void NavigateTo(string page)
    {
        CurrentPageName = page;
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
    }
}
