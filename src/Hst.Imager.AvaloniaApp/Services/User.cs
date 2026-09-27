using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading.Tasks;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.Models;
using Serilog;

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
    /// Returns false if the platform is unsupported, elevation was declined or launch failed.
    /// </summary>
    public static bool Elevate(string[] args, Settings settings) =>
        ElevateAsync(args, settings).GetAwaiter().GetResult();

    /// <summary>
    /// Restarts the current process with elevated privileges and exits the current instance.
    /// Returns false if the platform is unsupported, elevation was declined or launch failed.
    /// </summary>
    public static async Task<bool> ElevateAsync(string[] args, Settings settings)
    {
        var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exe)) return false;

        try
        {
            if (Hst.Core.OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(exe)
                {
                    Verb = "runas",
                    UseShellExecute = true,
                    Arguments = JoinArguments(args)
                });
            }
            else if (Hst.Core.OperatingSystem.IsMacOs())
            {
                if (!await ElevateMacOsAsync(exe, args, settings))
                {
                    return false;
                }
            }
            else
            {
                // elevate same way as gui app starts its elevated worker (pkexec on linux)
                var processStartInfo = ElevateHelper.GetElevatedProcessStartInfo(
                    $"{Constants.AppName} needs administrator privileges for raw disk access", exe,
                    JoinArguments(args), Path.GetDirectoryName(exe), settings.DebugMode);

                ElevateHelper.StartElevatedProcess(processStartInfo);
            }

            Log.CloseAndFlush();
            Environment.Exit(0);
            return true;
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to restart with administrator privileges");
            return false;
        }
    }

    private static string JoinArguments(string[] args) =>
        string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));

    /// <summary>
    /// Start app as root on macOS using osascript with either administrator privileges prompt or sudo in terminal.
    /// Waits for osascript to finish and returns true, if the elevated app was started.
    /// </summary>
    private static async Task<bool> ElevateMacOsAsync(string exe, string[] args, Settings settings)
    {
        // marker file is deleted by elevated shell to confirm elevation succeeded,
        // as terminal sudo doesn't return exit code of sudo
        var markerPath = Path.GetTempFileName();

        try
        {
            var prompt = $"{Constants.AppName} needs administrator privileges for raw disk access";
            var rootScript = CreateMacOsRootScript(exe, args, markerPath);

            var osaScriptLines = settings.MacOsElevateMethod == Settings.MacOsElevateMethodEnum.OsascriptSudo
                ? CreateMacOsTerminalSudoOsaScript(prompt, rootScript)
                : CreateMacOsAdministratorPrivilegesOsaScript(prompt, rootScript);

            var processStartInfo = new ProcessStartInfo("/usr/bin/osascript")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var line in osaScriptLines)
            {
                processStartInfo.ArgumentList.Add("-e");
                processStartInfo.ArgumentList.Add(line);
            }

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                return false;
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || File.Exists(markerPath))
            {
                Log.Information(
                    "Restart with administrator privileges was declined or failed, osascript exit code {ExitCode}",
                    process.ExitCode);
                return false;
            }

            return true;
        }
        finally
        {
            try
            {
                File.Delete(markerPath);
            }
            catch
            {
                // ignore
            }
        }
    }

    /// <summary>
    /// Create shell script run as root, which starts app detached with current users home, so settings and license
    /// agreement are shared with non elevated app. When app exits, ownership of app data is restored to current user,
    /// so non elevated app can still write settings and logs.
    /// </summary>
    private static string CreateMacOsRootScript(string exe, string[] args, string markerPath)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appDataPath = ApplicationDataHelper.GetApplicationDataDir("HstImager");
        var command = string.Join(" ", new[] { exe }.Concat(args).Select(ShellQuote));

        return string.Join(" ",
            // ignore hangup, so app keeps running when terminal window is closed
            "trap '' HUP;",
            $"rm -f {ShellQuote(markerPath)};",
            $"(HOME={ShellQuote(home)} {command}; chown -R {getuid()}:{getgid()} {ShellQuote(appDataPath)})",
            "</dev/null >/dev/null 2>&1 &");
    }

    private static IEnumerable<string> CreateMacOsAdministratorPrivilegesOsaScript(string prompt, string rootScript) =>
    [
        $"do shell script {AppleScriptQuote(rootScript)} with prompt {AppleScriptQuote(prompt)} with administrator privileges"
    ];

    private static IEnumerable<string> CreateMacOsTerminalSudoOsaScript(string prompt, string rootScript)
    {
        var terminalScript = $"clear; echo {ShellQuote(prompt)}; sudo /bin/sh -c {ShellQuote(rootScript)}; exit";

        return
        [
            "tell application \"Terminal\"",
            "activate",
            $"set w to do script {AppleScriptQuote(terminalScript)}",
            // wait until sudo and script is done
            "repeat",
            "delay 1",
            "if not busy of w then exit repeat",
            "end repeat",
            "set windowId to id of front window",
            "close window id windowId",
            "end tell"
        ];
    }

    private static string ShellQuote(string value) => $"'{value.Replace("'", "'\\''")}'";

    private static string AppleScriptQuote(string value) =>
        $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    [DllImport("libc")]
    private static extern uint getuid();

    [DllImport("libc")]
    private static extern uint getgid();
}
