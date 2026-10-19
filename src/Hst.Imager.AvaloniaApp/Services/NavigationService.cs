using System;

namespace Hst.Imager.AvaloniaApp.Services;

public class NavigationService : INavigationService
{
    public event Action<string>? NavigationRequested;

    public void NavigateTo(string page) => NavigationRequested?.Invoke(page);
}
