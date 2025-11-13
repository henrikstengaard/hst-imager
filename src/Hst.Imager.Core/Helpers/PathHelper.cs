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
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            var dirName = Path.GetDirectoryName(path) ?? string.Empty;
            var fileName = Path.GetFileName(path) ?? string.Empty;

            return Path.Combine(Path.GetFullPath(string.IsNullOrEmpty(dirName)
                ? Directory.GetCurrentDirectory() : dirName), fileName);
        }
    }
}