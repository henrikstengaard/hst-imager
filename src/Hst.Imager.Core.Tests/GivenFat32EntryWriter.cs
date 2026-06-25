using System;
using System.Linq;
using System.Text;
using Hst.Imager.Core.FileSystems.Fat;
using Hst.Imager.Core.FileSystems.Fat32;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenFatEntryWriter
{
    [Fact]
    public void When_BuildFatEntry_Then_FatEntryIsBuiltCorrectly()
    {
        // arrange - fat entry
        var fatEntry = new FatEntry
        {
            Name = "DISK",
            Attribute = 0x8,
            CreationDate = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc),
        };

        // act - build fat entry bytes
        var fatEntryBytes = FatEntryWriter.Build(fatEntry);

        // assert - name is written correctly
        Assert.Equal(Encoding.ASCII.GetBytes("DISK       "), fatEntryBytes.Take(11));

        // assert - creation date is written correctly
        var time = (fatEntry.CreationDate.Hour << 11) |
                   (fatEntry.CreationDate.Minute << 5) |
                   (fatEntry.CreationDate.Second / 2);
        Assert.Equal(time & 0xff, fatEntryBytes[0x16]);
        Assert.Equal(time >> 8, fatEntryBytes[0x17]);

        // assert - creation time is written correctly
        var date = ((fatEntry.CreationDate.Year - 1980) << 9) |
                   (fatEntry.CreationDate.Month << 5) |
                   fatEntry.CreationDate.Day;
        Assert.Equal(date & 0xff, fatEntryBytes[0x18]);
        Assert.Equal(date >> 8, fatEntryBytes[0x19]);
    }
}