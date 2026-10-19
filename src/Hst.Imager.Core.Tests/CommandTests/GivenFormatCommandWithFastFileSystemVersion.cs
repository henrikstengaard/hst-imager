using Hst.Imager.Core.Commands;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests
{
    public class GivenFormatCommandWithFastFileSystemVersion
    {
        [Theory]
        [InlineData(40, 63)]
        [InlineData(45, 13)]
        [InlineData(45, 20)]
        [InlineData(46, 12)]
        public void When_FastFileSystemIsOlderThanV46_13_Then_Dos7IsNotSupported(int version, int revision)
        {
            // act
            var hasDos7Support = FormatCommand.HasFastFileSystemDos7Support(version, revision);

            // assert
            Assert.False(hasDos7Support);
        }

        [Theory]
        [InlineData(46, 13)]
        [InlineData(46, 20)]
        [InlineData(47, 0)]
        [InlineData(47, 4)]
        public void When_FastFileSystemIsV46_13OrNewer_Then_Dos7IsSupported(int version, int revision)
        {
            // act
            var hasDos7Support = FormatCommand.HasFastFileSystemDos7Support(version, revision);

            // assert
            Assert.True(hasDos7Support);
        }
    }
}
