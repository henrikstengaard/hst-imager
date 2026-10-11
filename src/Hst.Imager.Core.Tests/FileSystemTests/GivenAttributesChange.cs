using System.IO;
using Hst.Amiga.FileSystems;
using Hst.Imager.Core.FileSystems;
using Hst.Imager.Core.Models;
using Xunit;

namespace Hst.Imager.Core.Tests.FileSystemTests;

public class GivenAttributesChange
{
    [Fact]
    public void When_ApplyingAddAndRemoveAmigaProtectionBits_Then_ProtectionBitsAreChanged()
    {
        // arrange - parse attributes change adding script and removing delete
        var result = AttributesChange.Parse(AttributesMode.Amiga, ["+s", "-d"]);
        Assert.True(result.IsSuccess);

        // act - apply attributes change to read, write, execute and delete protection bits
        var protectionBits = (ProtectionBits)result.Value.Apply((int)(ProtectionBits.Read | ProtectionBits.Write |
                                                                       ProtectionBits.Executable | ProtectionBits.Delete));

        // assert - protection bits has script and delete is removed
        Assert.Equal(ProtectionBits.Script | ProtectionBits.Read | ProtectionBits.Write | ProtectionBits.Executable,
            protectionBits);
    }

    [Fact]
    public void When_ApplyingCombinedAddAndRemoveInOneToken_Then_ProtectionBitsAreChanged()
    {
        // arrange - parse attributes change adding pure and removing write and delete in one token
        var result = AttributesChange.Parse(AttributesMode.Amiga, ["+p-wd"]);
        Assert.True(result.IsSuccess);

        // act - apply attributes change to read, write, execute and delete protection bits
        var protectionBits = (ProtectionBits)result.Value.Apply((int)(ProtectionBits.Read | ProtectionBits.Write |
                                                                       ProtectionBits.Executable | ProtectionBits.Delete));

        // assert - protection bits has pure and write and delete are removed
        Assert.Equal(ProtectionBits.Pure | ProtectionBits.Read | ProtectionBits.Executable, protectionBits);
    }

    [Fact]
    public void When_ApplyingExactAmigaProtectionBits_Then_OnlyExactProtectionBitsAreSet()
    {
        // arrange - parse attributes change setting read and execute exactly
        var result = AttributesChange.Parse(AttributesMode.Amiga, ["RE"]);
        Assert.True(result.IsSuccess);

        // act - apply attributes change to script, archive, read, write, execute and delete protection bits
        var protectionBits = (ProtectionBits)result.Value.Apply((int)(ProtectionBits.Script | ProtectionBits.Archive |
                                                                       ProtectionBits.Read | ProtectionBits.Write |
                                                                       ProtectionBits.Executable | ProtectionBits.Delete));

        // assert - protection bits only has read and execute
        Assert.Equal(ProtectionBits.Read | ProtectionBits.Executable, protectionBits);
    }

    [Fact]
    public void When_ApplyingWindowsAttributes_Then_OtherAttributesArePreserved()
    {
        // arrange - parse attributes change adding read only and hidden and removing archive
        var result = AttributesChange.Parse(AttributesMode.Windows, ["+rh", "-a"]);
        Assert.True(result.IsSuccess);

        // act - apply attributes change to directory with archive attribute
        var attributes = (FileAttributes)result.Value.Apply((int)(FileAttributes.Directory | FileAttributes.Archive));

        // assert - attributes has directory, read only and hidden
        Assert.Equal(FileAttributes.Directory | FileAttributes.ReadOnly | FileAttributes.Hidden, attributes);
    }

    [Fact]
    public void When_ApplyingWindowsAttributeToNormalAttributes_Then_NormalAttributeIsRemoved()
    {
        // arrange - parse attributes change adding hidden
        var result = AttributesChange.Parse(AttributesMode.Windows, ["+h"]);
        Assert.True(result.IsSuccess);

        // act - apply attributes change to normal attributes
        var attributes = (FileAttributes)result.Value.Apply((int)FileAttributes.Normal);

        // assert - attributes only has hidden
        Assert.Equal(FileAttributes.Hidden, attributes);
    }

    [Fact]
    public void When_ApplyingOctalUnixFileMode_Then_FileModeIsSet()
    {
        // arrange - parse attributes change with octal file mode 750
        var result = AttributesChange.Parse(AttributesMode.Unix, ["750"]);
        Assert.True(result.IsSuccess);

        // act - apply attributes change to file mode 777
        var unixFileMode = (UnixFileMode)result.Value.Apply((int)(UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                                                   UnixFileMode.UserExecute | UnixFileMode.GroupRead |
                                                                   UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                                                                   UnixFileMode.OtherRead | UnixFileMode.OtherWrite |
                                                                   UnixFileMode.OtherExecute));

        // assert - file mode is 750
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                     UnixFileMode.GroupRead | UnixFileMode.GroupExecute, unixFileMode);
    }

    [Fact]
    public void When_ApplyingRemoveUnixExecute_Then_ExecuteIsRemovedForUserGroupAndOther()
    {
        // arrange - parse attributes change removing execute
        var result = AttributesChange.Parse(AttributesMode.Unix, ["-x"]);
        Assert.True(result.IsSuccess);

        // act - apply attributes change to file mode 755
        var unixFileMode = (UnixFileMode)result.Value.Apply((int)(UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                                                   UnixFileMode.UserExecute | UnixFileMode.GroupRead |
                                                                   UnixFileMode.GroupExecute | UnixFileMode.OtherRead |
                                                                   UnixFileMode.OtherExecute));

        // assert - file mode is 644
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead |
                     UnixFileMode.OtherRead, unixFileMode);
    }

    [Theory]
    [InlineData(AttributesMode.Amiga, "+x")]
    [InlineData(AttributesMode.Windows, "+p")]
    [InlineData(AttributesMode.Unix, "+s")]
    public void When_ParsingInvalidAttribute_Then_ErrorIsReturned(AttributesMode mode, string token)
    {
        // act - parse invalid attribute
        var result = AttributesChange.Parse(mode, [token]);

        // assert - result is faulted
        Assert.True(result.IsFaulted);
    }

    [Fact]
    public void When_ParsingNoAttributes_Then_ErrorIsReturned()
    {
        // act - parse no attributes
        var result = AttributesChange.Parse(AttributesMode.Amiga, []);

        // assert - result is faulted
        Assert.True(result.IsFaulted);
    }
}
