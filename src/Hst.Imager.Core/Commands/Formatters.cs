namespace Hst.Imager.Core.Commands;

using Hst.Core;

public static class Formatters
{
    public static string FormatPhysicalDrivePath(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return path;
        }
        
        var diskPathMatch = Regexs.DiskPathRegex.Match(path);
        return diskPathMatch.Success ? $"\\\\.\\PhysicalDrive{diskPathMatch.Groups[1].Value}" : path;
    }

    public static string FormatDiskPath(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return path;
        }
        
        var physicalDrivePathMatch = Regexs.PhysicalDrivePathRegex.Match(path);
        return physicalDrivePathMatch.Success ? $"Disk{physicalDrivePathMatch.Groups[2].Value}" : path;
    }
}