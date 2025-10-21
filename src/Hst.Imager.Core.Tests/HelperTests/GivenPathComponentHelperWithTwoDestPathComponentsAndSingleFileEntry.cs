using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;
using Xunit;

namespace Hst.Imager.Core.Tests.HelperTests;

public class GivenPathComponentHelperWithTwoDestPathComponentsAndSingleFileEntry
{
    [Fact]
    public void When_GetFullPathComponents_Then_DirAndEntryPathComponentsAreCombinedX3()
    {
        // arrange - src and dest path components
        var srcPathComponents = new[] { "file1.txt" };
        var destPathComponents = new[] { "dir4", "dir5" };

        const bool lastRootPathComponentExist = false;
        const bool singleEntry = true;

        // act - get full path components
        var fullPathComponents = PathComponentHelper.GetFullPathComponents(EntryType.File, srcPathComponents, 
            destPathComponents, lastRootPathComponentExist, singleEntry);

        // assert - full path components should be just the root path component when it's a single entry operation
        Assert.Equal(["dir4", "dir5"], fullPathComponents);
    }

}