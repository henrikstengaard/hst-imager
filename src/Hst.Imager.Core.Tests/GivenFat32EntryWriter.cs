using System;
using System.Linq;
using System.Text;
using Hst.Imager.Core.FileSystems.Fat32;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenFat32EntryWriter
{
    [Fact]
    public void When_BuildFat32Entry_Then_Fat32EntryIsBuiltCorrectly()
    {
        // arrange - fat32 entry
        var fat32Entry = new Fat32Entry
        {
            Name = "DISK",
            Attribute = 0x8,
            CreationDate = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc),
        };
        
        // act - build fat32 entry bytes
        var fat32EntryBytes = Fat32EntryWriter.Build(fat32Entry);
        
        // assert - name is written correctly
        Assert.Equal(Encoding.ASCII.GetBytes("DISK       "), fat32EntryBytes.Take(11));

        // assert - creation date is written correctly
        var time = (fat32Entry.CreationDate.Hour << 11) |
                   (fat32Entry.CreationDate.Minute << 5) |
                   (fat32Entry.CreationDate.Second / 2);
        Assert.Equal(time & 0xff, fat32EntryBytes[0x16]);
        Assert.Equal(time >> 8, fat32EntryBytes[0x17]);
        
        // assert - creation time is written correctly
        var date = ((fat32Entry.CreationDate.Year - 1980) << 9) |
                   (fat32Entry.CreationDate.Month << 5) |
                   fat32Entry.CreationDate.Day;
        Assert.Equal(date & 0xff, fat32EntryBytes[0x18]);
        Assert.Equal(date >> 8, fat32EntryBytes[0x19]);
    }

    [Theory]
    [InlineData("DISK", "DISK       ")]
    [InlineData("DISK.", "DISK       ")]
    [InlineData(".DISK", "DISK       ")]
    [InlineData("New DISK", "NEWDISK    ")]
    [InlineData("DISK[1].2+2", "DISK122    ")]
    [InlineData("new.DISK.TMP", "NEWDISKTMP ")]
    [InlineData("very long disk name", "VERYLONGDIS")]
    public void When_BuildFat32EntryWithNameContainingInvalidChars_Then_InvalidCharsAreRemoved(string name, string expectedName)
    {
        // arrange - fat32 entry
        var fat32Entry = new Fat32Entry
        {
            Name = name,
            Attribute = 0x8,
            CreationDate = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc),
        };
        
        // act - build fat32 entry bytes
        var fat32EntryBytes = Fat32EntryWriter.Build(fat32Entry);
        
        // assert - name is written correctly
        Assert.Equal(Encoding.ASCII.GetBytes(expectedName), fat32EntryBytes.Take(11));
    }
}