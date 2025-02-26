namespace Hst.Imager.Core.Tests
{
    using Helpers;
    using Xunit;

    public class GivenElevateHelper
    {
        [Fact]
        public void When_CreateWindowsRunasProcessStartInfo_Then_ArgumentsWillElevateCommand()
        {
            var command = "hst-imager";
            var arguments = string.Empty;
            var workingDirectory = @"c:\program files\hst-imager";
            var processStartInfo =
                ElevateHelper.CreateWindowsRunasProcessStartInfo(command, arguments, workingDirectory);

            Assert.Equal(command, processStartInfo.FileName);
            Assert.Equal(workingDirectory, processStartInfo.WorkingDirectory);
            Assert.Equal(arguments, processStartInfo.Arguments);
            Assert.Equal("runas", processStartInfo.Verb);
        }

        [Fact]
        public void When_CreateLinuxPkExecProcessStartInfo_Then_ArgumentsWillElevateCommand()
        {
            var command = "hst-imager";
            var arguments = "--worker";
            var workingDirectory =
                "/home/hst";
            var processStartInfo =
                ElevateHelper.CreateLinuxPkExecProcessStartInfo(command, arguments, workingDirectory);
            
            Assert.Equal("/usr/bin/pkexec", processStartInfo.FileName);
            Assert.Equal(workingDirectory, processStartInfo.WorkingDirectory);
            Assert.Equal(
                $"bash -c \"./{command} {arguments}\"",
                processStartInfo.Arguments);
            Assert.Equal(string.Empty, processStartInfo.Verb);
        }

        [Fact]
        public void When_CreateMacOsProcessStartInfoWithAdministratorPrompt_Then_ArgumentsWillElevateCommand()
        {
            var prompt = "Hst Imager";
            var command = "hst-imager";
            var arguments = "--worker";
            var workingDirectory =
                "/home/hst";
            var processStartInfo =
                ElevateHelper.CreateMacOsProcessStartInfoWithAdministratorPrompt(prompt, command, arguments, workingDirectory);
            
            Assert.Equal("/usr/bin/osascript", processStartInfo.FileName);
            Assert.Equal(workingDirectory, processStartInfo.WorkingDirectory);
            Assert.Equal(
                $"-e \"do shell script \\\"./{command} {arguments} >/dev/null &\\\" with prompt \\\"{prompt}\\\" with administrator privileges\"",
                processStartInfo.Arguments);
            Assert.Equal(string.Empty, processStartInfo.Verb);
        }
        
        [Fact]
        public void When_CreateMacOsProcessStartInfoWithPromptAndNoWorkingDirectory_Then_ArgumentsWillElevateCommand()
        {
            var prompt = "Hst Imager";
            var command = "/home/hst/hst-imager";
            var arguments = "--worker";
            var workingDirectory = string.Empty;
            var processStartInfo =
                ElevateHelper.CreateMacOsProcessStartInfoWithAdministratorPrompt(prompt, command, arguments, workingDirectory);
            
            Assert.Equal("/usr/bin/osascript", processStartInfo.FileName);
            Assert.Equal(string.Empty, processStartInfo.WorkingDirectory);
            Assert.Equal(
                $"-e \"do shell script \\\"{command} {arguments} >/dev/null &\\\" with prompt \\\"{prompt}\\\" with administrator privileges\"",
                processStartInfo.Arguments);
            Assert.Equal(string.Empty, processStartInfo.Verb);
        }
        
        [Fact]
        public void When_CreateMacOsProcessStartInfoWithSudoAndNoWorkingDirectory_Then_ArgumentsWillElevateCommand()
        {
            var prompt = "Hst Imager";
            var command = "/home/hst/hst-imager";
            var arguments = "--worker";
            var workingDirectory = string.Empty;
            var processStartInfo =
                ElevateHelper.CreateMacOsProcessStartInfoWithSudo(prompt, command, arguments, workingDirectory);

            var script = $"echo '{prompt}'; sudo zsh -c '{command} {arguments} >/dev/null &'; exit";
            var osaScriptArgs = new[]
            {
                "-e \"tell application \\\"Terminal\\\"\"",
                "-e \"activate\"",
                $"-e \"set w to do script \\\"{script}\\\"\"",
                "-e \"repeat\"",
                "-e \"delay 1\"",
                "-e \"if not busy of w then exit repeat\"",
                "-e \"end repeat\"",
                "-e \"set windowId to id of front window\"",
                "-e \"close window id windowId\"",
                "-e \"end tell\""
            };
            
            Assert.Equal("/usr/bin/osascript", processStartInfo.FileName);
            Assert.Equal(string.Empty, processStartInfo.WorkingDirectory);
            Assert.Equal(
                string.Join(" ", osaScriptArgs),
                processStartInfo.Arguments);
            Assert.Equal(string.Empty, processStartInfo.Verb);
        }
    }
}