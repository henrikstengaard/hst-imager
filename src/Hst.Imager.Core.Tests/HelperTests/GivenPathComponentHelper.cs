using Hst.Imager.Core.Commands;
using Xunit;

namespace Hst.Imager.Core.Tests.HelperTests;

public class GivenPathComponentHelper
{
    [Fact]
    public void When_MatchingPathComponentsWithoutCaseSensitivity_Then_PathComponentsMatch()
    {
        // arrange - path components
        string[] pathComponents1 = ["dev", "kickstarts", "amigaos31.rom"];
        string[] pathComponents2 = ["dev", "KICKstarts", "amigaos31.ROM"];
        const bool caseSensitive = false;
        
        // act - match path components without case sensitivity
        var match = PathComponentHelper.MatchPathComponents(pathComponents1, pathComponents2, caseSensitive);
        
        // assert - path components match
        Assert.True(match.Success);
    }
    
    [Fact]
    public void When_MatchingPathComponentsWithCaseSensitivity_Then_PathComponentsDoesNotMatch()
    {
        // arrange - path components
        string[] pathComponents1 = ["dev", "kickstarts", "amigaos31.rom"];
        string[] pathComponents2 = ["dev", "KICKstarts", "amigaos31.ROM"];
        const bool caseSensitive = true;
        
        // act - match path components without case sensitivity
        var match = PathComponentHelper.MatchPathComponents(pathComponents1, pathComponents2, caseSensitive);
        
        // assert - path components do not match
        Assert.False(match.Success);
    }
}