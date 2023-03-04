namespace Hst.Imager.Core.Tests;

using Commands;
using Xunit;

public class GivenPatternMatcher
{
    [Fact]
    public void WhenPatternContainTextThenAllFileNamesOnlyContainingTextMatches()
    {
        var matcher = new PatternMatcher("new file");
        
        Assert.False(matcher.IsMatch("New"));
        Assert.False(matcher.IsMatch("New2"));
        Assert.True(matcher.IsMatch("New File"));
        Assert.False(matcher.IsMatch(" New"));
        Assert.False(matcher.IsMatch("Other File"));
    }
    
    [Fact]
    public void WhenPatternContainTextAndWildcardThenAllFileNamesStartingWithTextMatches()
    {
        var matcher = new PatternMatcher("new*");
        
        Assert.True(matcher.IsMatch("New"));
        Assert.True(matcher.IsMatch("New2"));
        Assert.True(matcher.IsMatch("New File"));
        Assert.False(matcher.IsMatch(" New"));
        Assert.False(matcher.IsMatch("Other File"));
    }
    
    [Fact]
    public void WhenPatternContainWildCardAndThenAllFileNamesEndingWithTextMatches()
    {
        var matcher = new PatternMatcher("*file");
        
        Assert.False(matcher.IsMatch("New"));
        Assert.False(matcher.IsMatch("New2"));
        Assert.True(matcher.IsMatch("New File"));
        Assert.False(matcher.IsMatch(" New"));
        Assert.True(matcher.IsMatch("Other File"));
    }
    
    [Fact]
    public void WhenPatternContainMultipleWildCardsThenFileNamesWithinWildCardsMatches()
    {
        var matcher = new PatternMatcher("*.*");
        
        Assert.False(matcher.IsMatch("New"));
        Assert.True(matcher.IsMatch("New2.png"));
        Assert.True(matcher.IsMatch("New File.txt"));
        Assert.False(matcher.IsMatch(" New"));
        Assert.False(matcher.IsMatch("Other File"));
    }
}