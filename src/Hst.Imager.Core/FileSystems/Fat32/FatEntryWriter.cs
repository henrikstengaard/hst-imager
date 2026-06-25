using System;
using System.Text;
using Hst.Imager.Core.FileSystems.Fat;

namespace Hst.Imager.Core.FileSystems.Fat32;

public static class FatEntryWriter
{
    public static byte[] Build(FatEntry fatEntry)
    {
        var fatEntryBytes = new byte[32];
        
        var nameBytes = Encoding.ASCII.GetBytes(fatEntry.Name);
        Array.Copy(nameBytes, 0, fatEntryBytes, 0, nameBytes.Length);

        if (nameBytes.Length < 11)
        {
            var fillBytes = new byte[11 - nameBytes.Length];
            Array.Fill<byte>(fillBytes, 0x20);
            Array.Copy(fillBytes, 0, fatEntryBytes, nameBytes.Length, fillBytes.Length);
        }
        
        fatEntryBytes[0xb] = fatEntry.Attribute;

        // creation time
        // bit    5432109876543210
        // 15-11  xxxxx            - hours (0-23)
        // 10-5        xxxxxx      - minutes (0-59)
        // 4-0               xxxxx - seconds / 2 (0-29)
        var time = (fatEntry.CreationDate.Hour << 11) |
                   (fatEntry.CreationDate.Minute << 5) |
                   (fatEntry.CreationDate.Second / 2);
        fatEntryBytes[0x16] = (byte)(time & 0xff); // time
        fatEntryBytes[0x17] = (byte)(time >> 8); // time

        // creation date
        // bit    5432109876543210
        // 15-9   xxxxxxx          - years (1980 + n, n = 0-127)
        // 8-5           xxxx      - months (1-12)
        // 4-0               xxxxx - days (1-31)
        var date = ((fatEntry.CreationDate.Year - 1980) << 9) |
                   (fatEntry.CreationDate.Month << 5) |
                   fatEntry.CreationDate.Day;
        fatEntryBytes[0x18] = (byte)(date & 0xff); // date
        fatEntryBytes[0x19] = (byte)(date >> 8); // date

        return fatEntryBytes;
    }
}