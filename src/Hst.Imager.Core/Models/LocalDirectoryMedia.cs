using System;
using System.IO;
using Hst.Imager.Core.FileInfos;
using Hst.Imager.Core.Helpers;

namespace Hst.Imager.Core.Models;

public class LocalDirectoryMedia : Media
{
    public readonly DriveInfo Drive;
        
    public LocalDirectoryMedia(string path, string name) 
        : base(path, name, MediaType.LocalDirectory, false, null, false)
    {
        Drive = new DriveInfo(PathHelper.GetFullPath(path));
    }

    public override bool Equals(Media other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (other is not LocalDirectoryMedia otherLocalDirectoryMedia)
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return Drive.Name.Equals(otherLocalDirectoryMedia.Drive.Name, StringComparison.OrdinalIgnoreCase);
        }

        // compare device id of paths, if stat is available for both paths.
        // otherwise fallback to comparing drive names.
        if (TryGetStat(Path, out var stat) &&
            TryGetStat(otherLocalDirectoryMedia.Path, out var otherStat))
        {
            return stat.DeviceId.Equals(otherStat.DeviceId);
        }

        return Drive.Name.Equals(otherLocalDirectoryMedia.Drive.Name, StringComparison.Ordinal);
    }

    private static bool TryGetStat(string path, out Stat stat)
    {
        if (OperatingSystem.IsLinux())
        {
            return LinuxFileInfo.TryGetStat(path, out stat);
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacOsFileInfo.TryGetStat(path, out stat);
        }

        stat = null;
        return false;
    }
}