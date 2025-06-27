using System;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;
using Xunit;

namespace Hst.Imager.Core.Tests.HelperTests;

public class GivenPathComponentHelperWithNoDestPathComponentsAndSingleFileEntry
{
    private const bool IsSingleFileEntryOperation = true;
    
    [Fact]
    public void When_GetFullPathComponentsForFileAndLastDestPathComponentExist_Then_FileNameIsNotChanged()
    {
        // arrange - src and dest path components
        var srcPathComponents = new []{"file1.txt"};
        var destPathComponents = Array.Empty<string>();

        // arrange - last root path component exists
        const bool doesLastPathComponentExist = true;
        
        // act - get full path components
        var fullPathComponents = PathComponentHelper.GetFullPathComponents(EntryType.File, srcPathComponents,
            destPathComponents, doesLastPathComponentExist, IsSingleFileEntryOperation);
        
        // assert - full path components is equal to dest path components concatenated with src path components
        // since last dest path component exists (copy without renaming)
        Assert.Equal(["file1.txt"], fullPathComponents);
    }

    [Fact]
    public void When_GetFullPathComponentsForFileAndLastDestPathComponentDoesntExist_Then_FileNameIsChanged()
    {
        // arrange - src and dest path components
        var srcPathComponents = new []{"file1.txt"};
        var destPathComponents = new []{"file2.txt"};
        
        // arrange - last root path component doesn't exist
        const bool doesLastPathComponentExist = false;
        
        // act - get full path components
        var fullPathComponents = PathComponentHelper.GetFullPathComponents(EntryType.File, srcPathComponents,
            destPathComponents, doesLastPathComponentExist, IsSingleFileEntryOperation);
        
        // assert - full path components is equal to dest path components
        // since last dest path component doesn't exists (copy with renaming)
        Assert.Equal(["file2.txt"], fullPathComponents);
    }
}