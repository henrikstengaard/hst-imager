using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;
using Xunit;

namespace Hst.Imager.Core.Tests.HelperTests;

public class GivenPathComponentHelperWithTwoDestPathComponentsAndSingleFileEntry
{
    [Fact]
    public void When_GetFullPathComponentsForSrcFileAndDestDirNotExisting_Then_DestPathIsReturned()
    {
        // arrange - src and dest path components
        const EntryType srcEntryType = EntryType.File;
        var srcPathComponents = new[] { "file1.txt" };
        const EntryType destEntryType = EntryType.Dir;
        var destPathComponents = new[] { "dir4", "dir5" };

        const bool lastRootPathComponentExist = false;
        const bool singleEntry = true;

        // act - get full path components
        var fullPathComponents = PathComponentHelper.GetFullPathComponents(srcEntryType, srcPathComponents, 
            destEntryType, destPathComponents, lastRootPathComponentExist, singleEntry);

        // assert - full path components should be just the root path component when it's a single entry operation
        Assert.Equal(["dir4", "dir5"], fullPathComponents);
    }

    [Fact]
    public void When_GetFullPathComponentsForSrcAndDestFileAndLastDestPathComponentExist_Then_DestFileIsReturned()
    {
        // arrange - src and dest path components
        const EntryType srcEntryType = EntryType.File;
        var srcPathComponents = new []{"dir1", "file1.txt"};
        const EntryType destEntryType = EntryType.File;
        var destPathComponents = new []{"file1.txt"};

        // arrange - last root path component exist
        const bool doesLastPathComponentExist = true;
        const bool singleEntry = true;

        // act - get full path components
        var fullPathComponents = PathComponentHelper.GetFullPathComponents(srcEntryType, srcPathComponents,
            destEntryType, destPathComponents, doesLastPathComponentExist, singleEntry);
        
        // assert - full path components is equal to dest path components
        // since last dest path component exists and src and dest is a file
        Assert.Equal(["file1.txt"], fullPathComponents);
    }
}