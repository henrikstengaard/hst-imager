namespace Hst.Imager.Core.Tests;

using System;
using Commands;
using Xunit;

public class GivenPathComponentMatcher
{
    [Fact]
    public void When_MatchingWithoutAnyPathComponents_Then_EqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcherV3(Array.Empty<string>());
        
        Assert.True(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "dir3" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir3" }));
    }

    [Fact]
    public void When_MatchingOnePathComponents_Then_EqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcherV3(new []{ "dir1" });
        
        Assert.True(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "dir3" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir3" }));
        Assert.False(matcher.IsMatch(new[] { "dir2" }));
    }
    
    [Fact]
    public void When_MatchingTwoPathComponents_Then_EqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcherV3(new []{ "dir1", "dir2" });
        
        Assert.False(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "dir3" }));
        Assert.False(matcher.IsMatch(new[] { "dir1", "dir3" }));
    }
    
    [Fact]
    public void When_MatchingOnePathComponentWithPatternAndRecursive_Then_EqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcherV3(new[] { "*.png" }, true);
        
        Assert.True(matcher.IsMatch(new[] { "file1.png" }));
        Assert.False(matcher.IsMatch(new[] { "file2.gif" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "file3.png" }));
        Assert.False(matcher.IsMatch(new[] { "dir1", "dir2" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "dir3", "file4.png" }));
        Assert.False(matcher.IsMatch(new[] { "dir1", "dir3" }));
    }

    [Fact]
    public void When_MatchingTwoPathComponentsWithTxtPattern_Then_EqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcherV3(new[] { "dir1", "*.txt" }, false);

        Assert.False(matcher.IsMatch(new[] { "file1.txt" }));
        Assert.False(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "file1.txt" }));
        Assert.False(matcher.IsMatch(new[] { "dir1", "image.gif" }));
        Assert.False(matcher.IsMatch(new[] { "dir1", "dir2", "file2.txt" }));
    }

    [Fact]
    public void When_MatchingOnePathComponentsWithAnyPattern_Then_EqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcherV3(new[] { "*" }, true);

        Assert.True(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir3" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "file1.txt" }));
        Assert.True(matcher.IsMatch(new[] { "dir2" }));
    }

    [Fact]
    public void When_MatchingTwoPathComponentsWithAnyPattern_Then_EqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcherV3(new []{ "dir1", "*" });
        
        Assert.True(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "dir3" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir3" }));
        Assert.False(matcher.IsMatch(new[] { "dir2" }));
        Assert.False(matcher.IsMatch(new[] { "dir3" }));
    }
    
    [Fact]
    public void When_MatchingThreePathComponentsWithAnyPattern_Then_EqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcherV3(new []{ "dir1", "dir4", "*" });
        
        Assert.False(matcher.IsMatch(new[] { "dir1" }));
        Assert.False(matcher.IsMatch(new[] { "dir1", "dir2" }));
        Assert.False(matcher.IsMatch(new[] { "dir1", "dir2", "dir3" }));
        Assert.False(matcher.IsMatch(new[] { "dir1", "dir3" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir4" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir4", "dir5" }));
        Assert.False(matcher.IsMatch(new[] { "dir2" }));
        Assert.False(matcher.IsMatch(new[] { "dir3" }));
    }
    
    [Fact]
    public void When_MatchingTwoPathComponentsWithPngPatternAndRecursive_Then_EqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcherV3(new []{ "dir1", "*.png" }, true);
        
        Assert.False(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "file1.png" }));
        Assert.False(matcher.IsMatch(new[] { "dir1", "dir2", "file2.gif" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "file3.png" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "file4.png" }));
        Assert.False(matcher.IsMatch(new[] { "file5.png" }));
        Assert.False(matcher.IsMatch(new[] { "dir2", "file6.png" }));
    }
}