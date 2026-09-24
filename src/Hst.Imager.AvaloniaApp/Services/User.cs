using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

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
    public static bool Elevate(string[] args)
    {
        var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exe)) return false;

        try
        {
            if (Hst.Core.OperatingSystem.IsWindows())
            {
                var psi = new ProcessStartInfo(exe)
                {
                    Verb = "runas",
                    UseShellExecute = true,
                    Arguments = string.Join(" ", args)
                };
                Process.Start(psi);
            }
            else
            {
                // Try pkexec first (polkit GUI prompt), fall back to nothing
                var launcher = FindExecutable("pkexec") ?? FindExecutable("sudo");
                if (launcher == null) return false;

                var psi = new ProcessStartInfo(launcher)
                {
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
                psi.ArgumentList.Add(exe);
                foreach (var a in args) psi.ArgumentList.Add(a);
                Process.Start(psi);
            }

            Environment.Exit(0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindExecutable(string name)
    {
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(':') ?? Array.Empty<string>();
        foreach (var dir in paths)
        {
            var full = System.IO.Path.Combine(dir, name);
            if (System.IO.File.Exists(full)) return full;
        }
        return null;
    }
}
