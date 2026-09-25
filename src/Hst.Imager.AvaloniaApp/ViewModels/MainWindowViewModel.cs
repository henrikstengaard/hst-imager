using System;
using System.Reactive;
using Hst.Imager.AvaloniaApp.Services;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly string[] _startupArgs;
    private ViewModelBase _currentPage;

    public MainWindowViewModel(IServiceProvider services, string[] startupArgs)
    {
        _services = services;
        _startupArgs = startupArgs;
        _currentPage = services.GetRequiredService<StartViewModel>();
        Progress = services.GetRequiredService<ProgressViewModel>();

        services.GetRequiredService<INavigationService>().NavigationRequested += NavigateTo;

        NavigateToCommand = ReactiveCommand.Create<string>(NavigateTo);
        ElevateCommand = ReactiveCommand.Create(Elevate);
    }

    public ProgressViewModel Progress { get; }

    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        private set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    public bool IsElevated { get; } = User.IsAdministrator();

    public ReactiveCommand<string, Unit> NavigateToCommand { get; }
    public ReactiveCommand<Unit, Unit> ElevateCommand { get; }

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
    }

    private void Elevate()
    {
        User.Elevate(_startupArgs, _services.GetRequiredService<Models.AppStateModel>().Settings);
    }
}
