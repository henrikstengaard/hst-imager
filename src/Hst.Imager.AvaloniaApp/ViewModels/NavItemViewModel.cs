using ReactiveUI;
using ReactiveUI.Reactive;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class NavItemViewModel(string page, string title, string icon) : ReactiveObject
{
    private bool _isActive;

    public string Page { get; } = page;
    public string Title { get; } = title;
    public string Icon { get; } = icon;

    public bool IsActive
    {
        get => _isActive;
        set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }
}
