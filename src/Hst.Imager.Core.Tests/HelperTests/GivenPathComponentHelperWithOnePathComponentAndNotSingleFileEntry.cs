using System;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;
using Xunit;

namespace Hst.Imager.Core.Tests.HelperTests;

public class GivenPathComponentHelperWithOnePathComponentAndNotSingleFileEntry
{
    private const bool IsSingleEntryOperation = false;

    [Theory]
    [InlineData(EntryType.File, true)]
    [InlineData(EntryType.File, false)]
    [InlineData(EntryType.Dir, true)]
    [InlineData(EntryType.Dir, false)]
    public void When_GetPathComponents_Then_DirAndEntryPathComponentsAreCombined(EntryType entryType,
        bool lastRootPathComponentExist)
    {
        // arrange - src and dest path components
        var srcPathComponents = new[] { "dir1" };
        var destPathComponents = new[] { "dir2" };

        // act - get full path components
        var fullPathComponents = PathComponentHelper.GetFullPathComponents(entryType, srcPathComponents,
            destPathComponents, lastRootPathComponentExist, IsSingleEntryOperation);

        // assert
        Assert.Equal(["dir2", "dir1"], fullPathComponents);
    }
}