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

        public static string GetFullPath(string path) => Path.GetFullPath(ResolveUserProfilePath(path));
    }
}