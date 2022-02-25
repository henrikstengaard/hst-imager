﻿namespace HstWbInstaller.Imager.Core.Extensions
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;

    public static class ProcessExtensions
    {
        public static string RunProcess(this string command, string args = null)
        {
            var process = Process.Start(
                new ProcessStartInfo(command)
                {
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Arguments = args ?? string.Empty
                });
        
            if (process == null)
            {
                throw new IOException($"Failed to start process command '{command}' {(string.IsNullOrWhiteSpace(args) ? string.Empty : $" with arguments '{args}'")}");
            }
        
            var stdOut = process.StandardOutput.ReadToEnd();
#if DEBUG
            Console.WriteLine(stdOut);
            Debug.WriteLine(stdOut);
#endif
            return stdOut;
        }

        public static async Task<string> RunProcessAsync(this string command, string args = null)
        {
            var process = Process.Start(
                new ProcessStartInfo(command)
                {
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Arguments = args ?? string.Empty
                });
        
            if (process == null)
            {
                throw new IOException($"Failed to start process command '{command}' {(string.IsNullOrWhiteSpace(args) ? string.Empty : $" with arguments '{args}'")}");
            }
        
            var stdOut = await process.StandardOutput.ReadToEndAsync();
#if DEBUG
            Console.WriteLine(stdOut);
            Debug.WriteLine(stdOut);
#endif
            return stdOut;
        }
    }
}