using System.Security.Principal;

namespace Hst.Imager.AvaloniaApp.Services;

public static class User
{
    public static bool IsAdministrator()
    {
        if (!Hst.Core.OperatingSystem.IsWindows()) return false;

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
