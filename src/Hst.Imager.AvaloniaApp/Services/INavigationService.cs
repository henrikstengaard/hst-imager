using System;

namespace Hst.Imager.AvaloniaApp.Services;

public interface INavigationService
{
    event Action<string>? NavigationRequested;
    void NavigateTo(string page);
}
