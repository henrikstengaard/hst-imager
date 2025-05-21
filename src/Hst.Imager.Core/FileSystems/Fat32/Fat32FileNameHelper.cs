using System.Text.RegularExpressions;

namespace Hst.Imager.Core.FileSystems.Fat32;

public static class Fat32FileNameHelper
{
    /// <summary>
    /// Regular expression used to examine if volume label contain characters other than:
    /// 0～9 A～Z ! # $ % & ' ( ) - @ ^ _ ` { } ~ space
    /// </summary>
    private static readonly Regex NonValidVolumeLabelCharsRegex = new(@"[^a-z0-9!#\\$%&'\\(\\)\\-\\@\\^_`{}~ ]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string MakeValidVolumeLabel(string volumeLabel)
    {
        var validVolumeLabel = NonValidVolumeLabelCharsRegex
            .Replace(volumeLabel, string.Empty)
            .ToUpperInvariant();
        
        if (validVolumeLabel.Length > 11)
        {
            validVolumeLabel = validVolumeLabel[..11];
        }
        
        return validVolumeLabel;
    }
}