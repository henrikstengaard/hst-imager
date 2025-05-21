using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Hst.Imager.Core.FileSystems.Fat32;

public static class Fat32EntryWriter
{
    /// <summary>
    /// Regular expression used to examine if names contain characters other than:
    /// 0～9 A～Z ! # $ % & ' ( ) - @ ^ _ ` { } ~
    /// </summary>
    private static readonly Regex NonValidSfnCharsRegex = new(@"[^a-z0-9!#\\$%&'\\(\\)\\-\\@\\^_`{}~]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static byte[] Build(Fat32Entry fat32Entry)
    {
        var fat32EntryBytes = new byte[32];

        var name = NonValidSfnCharsRegex
            .Replace(fat32Entry.Name, string.Empty)
            .ToUpperInvariant();
        
        if (name.Length > 11)
        {
            name = name[..11];
        }
        
        var nameBytes = Encoding.ASCII.GetBytes(name);
        Array.Copy(nameBytes, 0, fat32EntryBytes, 0, nameBytes.Length);

        if (name.Length < 11)
        {
            var fillBytes = new byte[11 - nameBytes.Length];
            Array.Fill<byte>(fillBytes, 0x20);
            Array.Copy(fillBytes, 0, fat32EntryBytes, nameBytes.Length, fillBytes.Length);
        }
        
        fat32EntryBytes[0xb] = fat32Entry.Attribute;

        // creation time
        // bit    5432109876543210
        // 15-11  xxxxx            - hours (0-23)
        // 10-5        xxxxxx      - minutes (0-59)
        // 4-0               xxxxx - seconds / 2 (0-29)
        var time = (fat32Entry.CreationDate.Hour << 11) |
                   (fat32Entry.CreationDate.Minute << 5) |
                   (fat32Entry.CreationDate.Second / 2);
        fat32EntryBytes[0x16] = (byte)(time & 0xff); // time
        fat32EntryBytes[0x17] = (byte)(time >> 8); // time

        // creation date
        // bit    5432109876543210
        // 15-9   xxxxxxx          - years (1980 + n, n = 0-127)
        // 8-5           xxxx      - months (1-12)
        // 4-0               xxxxx - days (1-31)
        var date = ((fat32Entry.CreationDate.Year - 1980) << 9) |
                   (fat32Entry.CreationDate.Month << 5) |
                   fat32Entry.CreationDate.Day;
        fat32EntryBytes[0x18] = (byte)(date & 0xff); // date
        fat32EntryBytes[0x19] = (byte)(date >> 8); // date

        return fat32EntryBytes;
    }
}