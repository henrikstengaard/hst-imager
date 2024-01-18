using Hst.Imager.Core.FileSystems.Ext;
using Xunit;

namespace Hst.Imager.Core.Tests.FileSystemTests;

public class GivenLittleEndianConverter
{
    [Fact]
    public void WhenConvertBytesToUInt32ThenValueIsEqual()
    {
        var bytes = new byte[] { 0xc0, 0x53, 0x1d, 0 };
        var value = LittleEndianConverter.ConvertBytesToUInt32(bytes);
        Assert.Equal(0x001d53c0U, value);
    }
}