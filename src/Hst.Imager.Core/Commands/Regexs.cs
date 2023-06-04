namespace Hst.Imager.Core.Commands;

using System.Text.RegularExpressions;

public static class Regexs
{
    public static readonly Regex DiskPathRegex =
        new("^disk(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly Regex PhysicalDrivePathRegex =
        new("^(\\\\\\\\\\.\\\\PHYSICALDRIVE|//\\./PHYSICALDRIVE|disk)(\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    public static readonly Regex DevicePathRegex =
        new("^/dev", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    /// <summary>
    /// Windows reserved names: CON, PRN, AUX, NUL, COM1, COM2, COM3, COM4, COM5, COM6, COM7, COM8, COM9, LPT1, LPT2, LPT3, LPT4, LPT5, LPT6, LPT7, LPT8, and LPT9
    /// https://learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file#naming-conventions
    /// </summary>
    public static readonly Regex WindowsReservedNamesRegex = new(
        "^(CON|PRN|AUX|NUL|COM1|COM2|COM3|COM4|COM5|COM6|COM7|COM8|COM9|LPT1|LPT2|LPT3|LPT4|LPT5|LPT6|LPT7|LPT8|LPT9|NUL\\.txt)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
}