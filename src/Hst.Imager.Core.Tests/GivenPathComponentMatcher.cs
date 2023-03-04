namespace Hst.Imager.Core.Tests;

using System;
using Commands;
using Xunit;

public class GivenPathComponentMatcher
{
    
    [Fact]
    public void WhenMatchingWithoutAnyPathComponentsThenEqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcher(Array.Empty<string>());
        
        Assert.True(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "dir3" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir3" }));
    }

    [Fact]
    public void WhenMatchingOnePathComponentsThenEqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcher(new []{ "dir1" });
        
        Assert.True(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "dir3" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir3" }));
        Assert.False(matcher.IsMatch(new[] { "dir2" }));
    }
    
    [Fact]
    public void WhenMatchingTwoPathComponentsThenEqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcher(new []{ "dir1", "dir2" });
        
        Assert.False(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "dir3" }));
        Assert.False(matcher.IsMatch(new[] { "dir1", "dir3" }));
    }
    
    [Fact]
    public void WhenMatchingOnePathComponentAndPatternThenEqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcher(new []{ "dir1" }, "*");
        
        Assert.True(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "dir3" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir3" }));
        Assert.False(matcher.IsMatch(new[] { "dir2" }));
        Assert.False(matcher.IsMatch(new[] { "dir3" }));
    }
    
    [Fact]
    public void WhenMatchingTwoPathComponentsAndPatternThenEqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcher(new []{ "dir1", "dir4" }, "*");
        
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
    public void WhenMatchingOnePathComponentPatternAndRecursiveThenEqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcher(new []{ "dir1" }, "*.png", true);
        
        Assert.False(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "file1.png" }));
        Assert.False(matcher.IsMatch(new[] { "dir1", "dir2", "file2.gif" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "file3.png" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "file4.png" }));
        Assert.False(matcher.IsMatch(new[] { "file5.png" }));
        Assert.False(matcher.IsMatch(new[] { "dir2", "file6.png" }));
    }
}