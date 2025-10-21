using System;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;
using Xunit;

namespace Hst.Imager.Core.Tests.HelperTests;

public class GivenPathComponentHelperWithNoDestPathComponentsAndNotSingleFileEntry
{
    private const bool IsSingleEntryOperation = false;
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_GetFullPathComponentsForDir_Then_DirAndEntryPathComponentsAreCombined(bool lastRootPathComponentExist)
    {
        // arrange - src and dest path components
        var srcPathComponents = new[] { "dir1" };
        var destPathComponents = Array.Empty<string>();

        // act - get full path components
        var fullPathComponents = PathComponentHelper.GetFullPathComponents(EntryType.Dir, srcPathComponents,
            destPathComponents, lastRootPathComponentExist, IsSingleEntryOperation);

        // assert
        Assert.Equal(["dir1"], fullPathComponents);
    }
        
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void When_GetFullPathComponentsForFile_Then_DirAndEntryPathComponentsAreCombined(bool lastRootPathComponentExist)
    {
        // arrange - src and dest path components
        var srcPathComponents = new[] { "file1.txt" };
        var destPathComponents = Array.Empty<string>();

        // act - get full path components
        var fullPathComponents = PathComponentHelper.GetFullPathComponents(EntryType.File, srcPathComponents,
            destPathComponents, lastRootPathComponentExist, IsSingleEntryOperation);

        // assert
        Assert.Equal(["file1.txt"], fullPathComponents);
    }
}