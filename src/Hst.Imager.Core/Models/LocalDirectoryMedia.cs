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

        var stat = OperatingSystem.IsLinux()
            ? LinuxFileInfo.GetStat(Path)
            : MacOsFileInfo.GetStat(Path);
        var otherStat = OperatingSystem.IsLinux()
            ? LinuxFileInfo.GetStat(otherLocalDirectoryMedia.Path)
            : MacOsFileInfo.GetStat(otherLocalDirectoryMedia.Path);
        return stat.DeviceId.Equals(otherStat.DeviceId);
    }
}