using System.Reactive;
using System.Reflection;
using Hst.Imager.AvaloniaApp.Services;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class AboutViewModel : ViewModelBase
{
    private const string GitHubReleasesUrl = "https://github.com/henrikstengaard/hst-imager/releases";
    private const string GitHubIssuesUrl = "https://github.com/henrikstengaard/hst-imager/issues";

    public AboutViewModel(IDialogService dialogService)
    {
        OpenReleasesCommand = ReactiveCommand.CreateFromTask(() => dialogService.OpenExternalAsync(GitHubReleasesUrl));
        OpenIssuesCommand = ReactiveCommand.CreateFromTask(() => dialogService.OpenExternalAsync(GitHubIssuesUrl));
    }

    public string AppVersion { get; } = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
    public string VersionText => $"Hst Imager v{AppVersion}.";

    public ReactiveCommand<Unit, Unit> OpenReleasesCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenIssuesCommand { get; }
}
