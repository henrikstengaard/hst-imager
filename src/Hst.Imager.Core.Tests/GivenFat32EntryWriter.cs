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
            Name = Encoding.ASCII.GetBytes("DISK"),
            Attribute = 0x8,
            CreatedDate = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc),
        };

        // act - build fat entry bytes
        var fatEntryBytes = FatEntryWriter.Build(fatEntry);

        // assert - name is written correctly
        Assert.Equal(Encoding.ASCII.GetBytes("DISK       "), fatEntryBytes.Take(11));

        // assert - creation date is written correctly
        var time = (fatEntry.CreatedDate.Hour << 11) |
                   (fatEntry.CreatedDate.Minute << 5) |
                   (fatEntry.CreatedDate.Second / 2);
        Assert.Equal(time & 0xff, fatEntryBytes[0x16]);
        Assert.Equal(time >> 8, fatEntryBytes[0x17]);

        // assert - creation time is written correctly
        var date = ((fatEntry.CreatedDate.Year - 1980) << 9) |
                   (fatEntry.CreatedDate.Month << 5) |
                   fatEntry.CreatedDate.Day;
        Assert.Equal(date & 0xff, fatEntryBytes[0x18]);
        Assert.Equal(date >> 8, fatEntryBytes[0x19]);
    }
}