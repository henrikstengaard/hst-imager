using Hst.Imager.Core.Commands;

namespace Hst.Imager.Core.Helpers
{
    using System;
    using System.IO;

    public static class PathHelper
    {
        private static string ResolveUserProfilePath(string path) => 
            (path.Length >= 2 && path.StartsWith("~/"))
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.Substring(2))
            : path;

        public static string GetFullPath(string path)
        {
            path = ResolveUserProfilePath(path);
            
            var isMacOsOrLinux = OperatingSystem.IsMacOS() || OperatingSystem.IsLinux(); 
            
            // return path, if not macos or linux and path is full path
            if (!isMacOsOrLinux && path.Length > 0 && path.StartsWith("/"))
            {
                return path;
            }

            // return path, if not windows and path is full path
            if (!OperatingSystem.IsWindows() && path.Length > 1 &&
                (path.StartsWith(@"\") || Regexs.WindowsDriveRegex.IsMatch(path)))
            {
                return path;
            }

            // split path into directory and filename
            var dirName = Path.GetDirectoryName(path) ?? string.Empty;
            var fileName = Path.GetFileName(path) ?? string.Empty;

            // get full path for directory and combine with filename
            // main reason to not get full path for path, is because Windows 10
            // return "\\.\AUX" when path ends with filename "AUX".
            return Path.Combine(Path.GetFullPath(string.IsNullOrEmpty(dirName)
                ? Directory.GetCurrentDirectory() : dirName), fileName);
        }
    }
}