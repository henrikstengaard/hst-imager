namespace Hst.Imager.Core.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using OperatingSystem = Hst.Core.OperatingSystem;

    public static class ElevateHelper
    {
        private static IEnumerable<string> GetBashExports(string command)
        {
            var args = new List<string>(new[]
            {
                $"cd \"{Path.GetDirectoryName(command)}\";",
            });

            var environmentVariables = Environment.GetEnvironmentVariables();
            foreach (var key in environmentVariables.Keys)
            {
                args.Add($"export {key}=\"{environmentVariables[key]}\";");
            }

            return args;
        }

        public static string[] GetBashArgs(string command, string arguments = null, bool escapeQuotes = false)
        {
            var commandArgs = new List<string>();
            if (!string.IsNullOrWhiteSpace(Path.GetDirectoryName(command)))
            {
                commandArgs.Add($"cd \"{Path.GetDirectoryName(command)}\";");
            }

            commandArgs.Add(
                $"\".{Path.DirectorySeparatorChar}{Path.GetFileName(command)}\"{(string.IsNullOrWhiteSpace(arguments) ? string.Empty : $" {arguments}")};");

            var commandLineArg = string.Join(" ", commandArgs);
            return new[]
            {
                "/bin/bash",
                "-c",
                $"\"{(escapeQuotes ? commandLineArg.Replace("\"", "\\\"") : commandLineArg)}\""
            };
        }

        public static ProcessStartInfo GetKdeSudoProcessStartInfo(string applicationName, string command,
            string arguments = null)
        {
            var kdeSudoArgs = new List<string>(new[]
            {
                "--comment",
                $"\"'{applicationName}' wants to make changes. Enter your password to allow this.\"",
                "-d",
                "--"
            });

            var args = string.Join(" ", kdeSudoArgs.Concat(GetBashArgs(command, arguments)));

            return new ProcessStartInfo("/usr/bin/kdesudo")
            {
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = true,
                UseShellExecute = true,
                Arguments = args,
            };
        }

        /// <summary>
        /// create linux pkexec process start info to run command with administrator privileges
        /// </summary>
        /// <param name="command"></param>
        /// <param name="arguments"></param>
        /// <param name="workingDirectory"></param>
        /// <param name="showWindow"></param>
        /// <returns></returns>
        public static ProcessStartInfo CreateLinuxPkExecProcessStartInfo(string command, string arguments = null,
            string workingDirectory = null, bool showWindow = true)
        {
            var script =
                $"{(command.StartsWith("/") ? command : $"./{command}")}{(string.IsNullOrWhiteSpace(arguments) ? string.Empty : $" {arguments}")}";

            var bashArgs = new List<string>(new[]
            {
                "bash",
                "-c",
                $"\"{script}\""
            });

            var args = string.Join(" ", bashArgs);

            return new ProcessStartInfo("/usr/bin/pkexec")
            {
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = !showWindow,
                WindowStyle = showWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden,
                UseShellExecute = true,
                Arguments = args,
                WorkingDirectory = workingDirectory ?? string.Empty
            };
        }

        private static string CreateMacOsScript(string prompt, string command,
            string arguments = null, string workingDirectory = null,
            bool sudo = false, bool showWindow = false)
        {
            var scriptLines = new string[]{
                command.StartsWith("/") ? command : $"./{command}",
                string.IsNullOrWhiteSpace(arguments) ? string.Empty : $"{arguments}",
                !showWindow ? ">/dev/null &" : string.Empty
            };

            var script = string.Join(" ", scriptLines);

            if (!sudo)
            {
                return script;
            }

            var sudoScriptLines = new List<string>
            {
                $"echo '{prompt}'"
            };

            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                sudoScriptLines.Add($"cd '{workingDirectory}'");
            }

            sudoScriptLines.Add($"sudo zsh -c '{script}'; exit");

            return string.Join("; ", sudoScriptLines);
        }

        private static string CreateMacOsOsaScriptArgsWithAdministratorPrompt(string script, string prompt) => 
            $"-e \"do shell script \\\"{script}\\\" with prompt \\\"{prompt}\\\" with administrator privileges\"";

        private static string CreateMacOsOsaScriptArgsWithTerminalWindow(string script) => string.Join(" ",
        [
            // open new terminal window
            "-e \"tell application \\\"Terminal\\\"\"",

            // bring application to the front
            "-e \"activate\"",
            
            // run script
            $"-e \"set w to do script \\\"{script}\\\"\"",

            // repeat until script is done
            "-e \"repeat\"",
            "-e \"delay 1\"",
            "-e \"if not busy of w then exit repeat\"",
            "-e \"end repeat\"",

            // close terminal window
            "-e \"set windowId to id of front window\"",
            "-e \"close window id windowId\"",

            // end open new terminal window
            "-e \"end tell\""
        ]);

        /// <summary>
        /// create mac os osascript process start info to run command with administrator privileges
        /// </summary>
        /// <param name="prompt"></param>
        /// <param name="command"></param>
        /// <param name="arguments"></param>
        /// <param name="workingDirectory"></param>
        /// <param name="showWindow"></param>
        /// <returns></returns>
        public static ProcessStartInfo CreateMacOsProcessStartInfoWithAdministratorPrompt(string prompt, string command,
            string arguments = null, string workingDirectory = null)
        {
            var script = CreateMacOsScript(prompt, command, arguments, workingDirectory, false, false);

            var osaScriptArgs = CreateMacOsOsaScriptArgsWithAdministratorPrompt(script, prompt);

            return new ProcessStartInfo("/usr/bin/osascript")
            {
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = true, // create window is not supported with macos
                WindowStyle = ProcessWindowStyle.Normal,
                UseShellExecute = true,
                Arguments = osaScriptArgs,
                WorkingDirectory = workingDirectory ?? string.Empty
            };
        }

        /// <summary>
        /// create mac os osascript process start info to run command with terminal sudo
        /// </summary>
        /// <param name="prompt"></param>
        /// <param name="command"></param>
        /// <param name="arguments"></param>
        /// <param name="workingDirectory"></param>
        /// <returns></returns>
        public static ProcessStartInfo CreateMacOsProcessStartInfoWithSudo(string prompt, string command,
            string arguments = null, string workingDirectory = null, bool showWindow = false)
        {
            var script = CreateMacOsScript(prompt, command, arguments, workingDirectory,
                true, showWindow);

            var osaScriptArgs = CreateMacOsOsaScriptArgsWithTerminalWindow(script);

            return new ProcessStartInfo("/usr/bin/osascript")
            {
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = true, // create window is not supported with macos
                WindowStyle = ProcessWindowStyle.Normal,
                UseShellExecute = true,
                Arguments = osaScriptArgs,
                WorkingDirectory = workingDirectory ?? string.Empty
            };
        }

        /// <summary>
        /// create windows runas process start info to run command with administrator privileges
        /// </summary>
        /// <param name="command"></param>
        /// <param name="arguments"></param>
        /// <param name="workingDirectory"></param>
        /// <param name="showWindow"></param>
        /// <returns></returns>
        public static ProcessStartInfo CreateWindowsRunasProcessStartInfo(string command, string arguments = null,
            string workingDirectory = null, bool showWindow = false)
        {
            return new ProcessStartInfo(Path.GetFileName(command))
            {
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = !showWindow,
                WindowStyle = showWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden,
                UseShellExecute = true,
                Arguments = arguments ?? string.Empty,
                WorkingDirectory = workingDirectory ?? string.Empty,
                Verb = "runas"
            };
        }

        public static ProcessStartInfo GetElevatedProcessStartInfo(string prompt, string command,
            string arguments = null, string workingDirectory = null, bool showWindow = false, bool osaScriptSudo = false)
        {
            ProcessStartInfo processStartInfo;
            if (OperatingSystem.IsWindows())
            {
                processStartInfo = CreateWindowsRunasProcessStartInfo(command, arguments, workingDirectory, showWindow);
            }
            else if (OperatingSystem.IsMacOs())
            {
                processStartInfo = osaScriptSudo
                    ? CreateMacOsProcessStartInfoWithSudo(prompt, command, arguments, workingDirectory, showWindow)
                    : CreateMacOsProcessStartInfoWithAdministratorPrompt(prompt, command, arguments, workingDirectory);
            }
            else if (OperatingSystem.IsLinux())
            {
                processStartInfo = CreateLinuxPkExecProcessStartInfo(command, arguments, workingDirectory, showWindow);
            }
            else
            {
                throw new NotSupportedException("Operating system is not supported");
            }

            return processStartInfo;
        }

        public static Process StartElevatedProcess(ProcessStartInfo processStartInfo)
        {
            var process = Process.Start(processStartInfo);

            if (process == null)
            {
                throw new IOException(
                    $"Failed to start elevated process file name '{processStartInfo.FileName}' {(string.IsNullOrWhiteSpace(processStartInfo.Arguments) ? string.Empty : $" with arguments '{processStartInfo.Arguments}'")}");
            }

            return process;
        }
    }
}