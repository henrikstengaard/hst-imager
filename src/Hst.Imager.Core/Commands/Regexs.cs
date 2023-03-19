namespace Hst.Imager.Core.Commands;

using System.Text.RegularExpressions;

public static class Regexs
{
    public static readonly Regex PhysicalDrivePathRegex =
        new("^(\\\\\\\\\\.\\\\PHYSICALDRIVE|//\\./PHYSICALDRIVE)\\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    public static readonly Regex DevicePathRegex =
        new("^/dev", RegexOptions.IgnoreCase | RegexOptions.Compiled);
}