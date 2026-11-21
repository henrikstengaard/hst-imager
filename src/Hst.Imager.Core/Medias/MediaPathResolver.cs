namespace Hst.Imager.Core.Medias
{
    using System;
    using System.IO;
    using Commands;
    using Helpers;
    using Hst.Core;
    using Microsoft.Extensions.Logging;
    using OperatingSystem = Hst.Core.OperatingSystem;

    public class MediaPathResolver : IMediaPathResolver
    {
        private readonly ILogger logger;

        public MediaPathResolver(ILogger logger)
        {
            this.logger = logger;
        }

        public virtual Result<MediaResult> ResolveMedia(string path, bool allowNonExisting = false)
        {
            logger.LogDebug($"Resolving path '{path}'");

            var byteSwap = false;

            var modifiersResult = ResolveModifiers(path);

            if (modifiersResult.HasModifiers)
            {
                path = modifiersResult.Path;
                byteSwap = modifiersResult.Modifiers.HasFlag(ModifierEnum.ByteSwap);
            }

            logger.LogDebug($"Modifiers: '{modifiersResult.Modifiers}'");

            var diskPathMatch = Regexs.DiskPathRegex.Match(path);
            var physicalDrivePath = diskPathMatch.Success
                ? string.Concat($"\\\\.\\PhysicalDrive{diskPathMatch.Groups[2].Value}",
                    path.Substring(diskPathMatch.Groups[1].Value.Length + diskPathMatch.Groups[2].Value.Length))
                : path;

            var directorySeparatorChar = Path.DirectorySeparatorChar;

            for (var i = 0; i < path.Length; i++)
            {
                if (path[i] == '\\' || path[i] == '/')
                {
                    directorySeparatorChar = path[i];
                    break;
                }
            }

            string fileSystemPath;

            // physical drive
            var physicalDrivePathMatch = Regexs.PhysicalDrivePathRegex.Match(physicalDrivePath);
            if (physicalDrivePathMatch.Success)
            {
                var physicalDriveMediaPath = physicalDrivePathMatch.Value;
                var firstSeparatorIndex = physicalDrivePath.IndexOf(directorySeparatorChar.ToString(),
                    physicalDriveMediaPath.Length, StringComparison.Ordinal);

                fileSystemPath = firstSeparatorIndex >= 0
                        ? physicalDrivePath.Substring(firstSeparatorIndex + 1,
                            physicalDrivePath.Length - (firstSeparatorIndex + 1))
                        : string.Empty;

                logger.LogDebug($"Media Path: '{physicalDriveMediaPath}'");
                logger.LogDebug($"File system Path: '{fileSystemPath}'");

                return new Result<MediaResult>(new MediaResult
                {
                    Exists = true,
                    FullPath = path,
                    MediaPath = physicalDriveMediaPath,
                    FileSystemPath = fileSystemPath,
                    DirectorySeparatorChar = directorySeparatorChar.ToString(),
                    Modifiers = modifiersResult.Modifiers,
                    ByteSwap = byteSwap
                });
            }

            path = PathHelper.GetFullPath(path);

            // if path starts with a network path, update next to start after network path start
            var networkPath = GetNetworkPath(path);
            var next = string.IsNullOrWhiteSpace(networkPath) ? 0 : networkPath.Length;

            // detect if path contains a media file
            var existingMediaPath = string.Empty;
            do
            {
                next = path.IndexOf(directorySeparatorChar.ToString(), next + 1, StringComparison.OrdinalIgnoreCase);
                var mediaPath = path.Substring(0, next == -1 ? path.Length : next);

                if (File.Exists(mediaPath))
                {
                    fileSystemPath = mediaPath.Length + 1 < path.Length
                            ? path.Substring(mediaPath.Length + 1, path.Length - (mediaPath.Length + 1))
                            : string.Empty;

                    logger.LogDebug($"Media Path: '{mediaPath}'");
                    logger.LogDebug($"File system Path: '{fileSystemPath}'");

                    return new Result<MediaResult>(new MediaResult
                    {
                        Exists = true,
                        FullPath = path,
                        MediaPath = mediaPath,
                        FileSystemPath = fileSystemPath,
                        DirectorySeparatorChar = directorySeparatorChar.ToString(),
                        Modifiers = modifiersResult.Modifiers,
                        ByteSwap = byteSwap
                    });
                }

                if (!Directory.Exists(mediaPath))
                {
                    break;
                }

                existingMediaPath = mediaPath;
            } while (next != -1);

            if (!Directory.Exists(path) && !allowNonExisting)
            {
                return new Result<MediaResult>(new PathNotFoundError($"Path not found '{path}'", path));
            }

            fileSystemPath = existingMediaPath.Length + 1 < path.Length
                ? path.Substring(existingMediaPath.Length + 1, path.Length - (existingMediaPath.Length + 1))
                : string.Empty;

            logger.LogDebug($"Media Path: '{existingMediaPath}'");
            logger.LogDebug($"File system Path: '{fileSystemPath}'");

            return new Result<MediaResult>(new MediaResult
            {
                FullPath = path,
                MediaPath = existingMediaPath,
                FileSystemPath = fileSystemPath,
                DirectorySeparatorChar = directorySeparatorChar.ToString(),
                Modifiers = modifiersResult.Modifiers,
                ByteSwap = byteSwap
            });
        }

        private static string GetNetworkPath(string path)
        {
            var networkPathMatch = Regexs.NetworkPathRegex.Match(path);
            return networkPathMatch.Success ? networkPathMatch.Groups[1].Value : null;
        }

        /// <summary>
        /// Resolves modifiers in the start of the path, e.g. +bs:/path/to/file.
        /// </summary>
        /// <param name="path">Path to resolve modifiers from.</param>
        /// <returns>Result</returns>
        public virtual ModifierResult ResolveModifiers(string path)
        {
            if (path.Length == 0)
            {
                return new ModifierResult
                {
                    Path = path,
                    HasModifiers = false,
                    Modifiers = ModifierEnum.None
                };
            }

            var modifierMatch = Regexs.ModifiersRegex.Match(path.ToLower());

            if (!modifierMatch.Success)
            {
                return new ModifierResult
                {
                    Path = path,
                    HasModifiers = false,
                    Modifiers = ModifierEnum.None
                };
            }

            return new ModifierResult
            {
                Path = path.Substring(modifierMatch.Value.Length),
                HasModifiers = true,
                Modifiers = ModifierEnum.ByteSwap
            };
        }

        public virtual string GetPhysicalDrivePath(string path)
        {
            if (OperatingSystem.IsWindows())
            {
                var diskPathMatch = Regexs.DiskPathRegex.Match(path);
                if (diskPathMatch.Success)
                {
                    return $"\\\\.\\PhysicalDrive{diskPathMatch.Groups[2].Value}";
                }

                return Regexs.PhysicalDrivePathRegex.IsMatch(path) ? path : null;
            }

            if (OperatingSystem.IsMacOs() || OperatingSystem.IsLinux())
            {
                return Regexs.DevicePathRegex.IsMatch(path) ? path : null;
            }

            return null;
        }
    }
}
