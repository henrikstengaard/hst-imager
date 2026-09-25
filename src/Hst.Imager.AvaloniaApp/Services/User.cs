using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Principal;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;

namespace Hst.Imager.AvaloniaApp.Services;

public static class User
{
    public static bool IsAdministrator()
    {
        if (Hst.Core.OperatingSystem.IsWindows())
        {
            return IsWindowsAdministrator();
        }

        return Environment.GetEnvironmentVariable("USER") == "root"
               || Environment.GetEnvironmentVariable("SUDO_USER") != null
               || IsUnixRoot();
    }

    [SupportedOSPlatform("windows")]
    private static bool IsWindowsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
    
    private static bool IsUnixRoot()
    {
        try
        {
            var id = new ProcessStartInfo("id", "-u")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(id);
            if (proc == null) return false;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();
            return output == "0";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Restarts the current process with elevated privileges and exits the current instance.
    /// Returns true if elevation was initiated, false if the platform is unsupported or launch failed.
    /// </summary>
    public static bool Elevate(string[] args, Settings settings)
    {
        var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exe)) return false;

        try
        {
            var arguments = string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));

            if (Hst.Core.OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(exe)
                {
                    Verb = "runas",
                    UseShellExecute = true,
                    Arguments = arguments
                });
            }
            else
            {
                // elevate same way as gui app starts its elevated worker (pkexec on linux, osascript on macos)
                var processStartInfo = ElevateHelper.GetElevatedProcessStartInfo(
                    $"{Constants.AppName} needs administrator privileges for raw disk access", exe, arguments,
                    Path.GetDirectoryName(exe), settings.DebugMode,
                    settings.MacOsElevateMethod == Settings.MacOsElevateMethodEnum.OsascriptSudo);

                ElevateHelper.StartElevatedProcess(processStartInfo);
            }

            Environment.Exit(0);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
