using Hst.Imager.Core.FileSystems.Fat32;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenFat32FileNameHelper
{
    [Theory]
    [InlineData("DISK", "DISK")]
    [InlineData("DISK.", "DISK")]
    [InlineData(".DISK", "DISK")]
    [InlineData("New DISK", "NEW DISK")]
    [InlineData("DISK[1].2+2", "DISK122")]
    [InlineData("new.DISK.TMP", "NEWDISKTMP")]
    [InlineData("very long name", "VERY LONG N")]
    public void When_MakingValidVolumeLabel_Then_InvalidCharsAreRemoved(string volumeLabel, string expectedVolumeLabel)
    {
        // act - make valid volume label
        var actualVolumeLabel = Fat32FileNameHelper.MakeValidVolumeLabel(volumeLabel);
        
        // assert - volume label is valid
        Assert.Equal(expectedVolumeLabel, actualVolumeLabel);
    }
}