namespace Hst.Imager.Core.Tests;

using Commands;
using Xunit;

public class GivenFileNameMatcher
{
    [Fact]
    public void WhenPatternContainTextThenAllFileNamesOnlyContainingTextMatches()
    {
        var fileNameMatcher = new FileNameMatcher("new file");
        
        Assert.False(fileNameMatcher.IsMatch("New"));
        Assert.False(fileNameMatcher.IsMatch("New2"));
        Assert.True(fileNameMatcher.IsMatch("New File"));
        Assert.False(fileNameMatcher.IsMatch(" New"));
        Assert.False(fileNameMatcher.IsMatch("Other File"));
    }
    
    [Fact]
    public void WhenPatternContainTextAndWildcardThenAllFileNamesStartingWithTextMatches()
    {
        var fileNameMatcher = new FileNameMatcher("new*");
        
        Assert.True(fileNameMatcher.IsMatch("New"));
        Assert.True(fileNameMatcher.IsMatch("New2"));
        Assert.True(fileNameMatcher.IsMatch("New File"));
        Assert.False(fileNameMatcher.IsMatch(" New"));
        Assert.False(fileNameMatcher.IsMatch("Other File"));
    }
    
    [Fact]
    public void WhenPatternContainWildCardAndThenAllFileNamesEndingWithTextMatches()
    {
        var fileNameMatcher = new FileNameMatcher("*file");
        
        Assert.False(fileNameMatcher.IsMatch("New"));
        Assert.False(fileNameMatcher.IsMatch("New2"));
        Assert.True(fileNameMatcher.IsMatch("New File"));
        Assert.False(fileNameMatcher.IsMatch(" New"));
        Assert.True(fileNameMatcher.IsMatch("Other File"));
    }
    
    [Fact]
    public void WhenPatternContainMultipleWildCardsThenFileNamesWithinWildCardsMatches()
    {
        var fileNameMatcher = new FileNameMatcher("*.*");
        
        Assert.False(fileNameMatcher.IsMatch("New"));
        Assert.True(fileNameMatcher.IsMatch("New2.png"));
        Assert.True(fileNameMatcher.IsMatch("New File.txt"));
        Assert.False(fileNameMatcher.IsMatch(" New"));
        Assert.False(fileNameMatcher.IsMatch("Other File"));
    }
}