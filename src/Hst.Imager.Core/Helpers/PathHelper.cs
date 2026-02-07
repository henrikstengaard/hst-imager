using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Hst.Imager.Core.Commands;

namespace Hst.Imager.Core.Helpers
{
    using System;
    using System.IO;

    public static class PathHelper
    {
        public static string[] Split(string path) =>
            (path.StartsWith("/") ? new []{"/"} : Array.Empty<string>())
            .Concat(path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)).ToArray();

        public static string[] Split(string directorySeparatorChar, string path) =>
            (path.StartsWith("/") ? new []{"/"} : Array.Empty<string>())
            .Concat(path.Split(directorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)).ToArray();
        
        private static string ResolveUserProfilePath(string path) => 
            (path.Length >= 2 && path.StartsWith("~/"))
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.Substring(2))
            : path;

        public static bool IsRootPath(string path) =>
            IsMacOrLinuxRootPath(path) || IsWindowsRootPath(path);
        
        public static bool IsMacOrLinuxRootPath(string path) =>
            path.Length > 0 &&
            path.StartsWith("/");
        
        public static bool IsWindowsRootPath(string path) =>
            path.Length > 1 &&
            (path.StartsWith(@"\") || Regexs.WindowsDriveRegex.IsMatch(path));

        public static string GetFullPath(string path)
        {
            path = ResolveUserProfilePath(path);
            
            // return path, if root path
            if (IsRootPath(path))
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

        /// <summary>
        /// Get temporary name for given path using SHA-256 ensures a strong, collision-resistant base hash and
        /// base64 url-safe encoding to makes it shorter and safe for filenames/URLs.
        /// </summary>
        /// <param name="path">Path to create temporary name for.</param>
        /// <param name="length">Length of temporary name.</param>
        /// <returns>Temporary name.</returns>
        public static string GetTempName(string path, int length = 10)
        {
            // create sha256 hash
            using var sha256 = SHA256.Create();

            // get bytes for path
            var bytes = Encoding.UTF8.GetBytes(path);

            // compute hash bytes from bytes
            var hashBytes = sha256.ComputeHash(bytes);
            
            // base64 encode the hash bytes
            var base64 = Convert.ToBase64String(hashBytes);

            // make base64 url-safe replacing '+' with '-', '/' with '_', and remove '=' padding
            var urlSafeBase64 = base64.Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
            
            // return requested length
            return urlSafeBase64.Substring(0, Math.Min(length, urlSafeBase64.Length));
        }
        
        /// <summary>
        /// Get layer path for given path.
        /// </summary>
        /// <param name="path">Path to create layer path for.</param>
        /// <returns>Layer path.</returns>
        public static string GetLayerPath(string path)
        {
            return Path.GetFullPath($"hst-imager.layer-{GetTempName(path)}.bin");
        }
    }
}