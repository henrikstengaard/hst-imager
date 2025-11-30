namespace Hst.Imager.Core.Tests;

using System;
using Hst.Imager.Core.PathComponents;
using Xunit;

public class GivenPathComponentMatcher
{
    [Fact]
    public void When_MatchingTwoPathComponentsFileRecursive_Then_EqualOrMorePathComponentsMatch()
    {
        // arrange
        const bool isFile = true;
        const bool recursive = true;
        var matcher = new PathComponentMatcher(["dir1", "file1.txt"], isFile: isFile, recursive: recursive);

        // act and assert
        Assert.True(matcher.IsMatch(["dir1", "dir3", "file1.txt"]));
        Assert.True(matcher.IsMatch(["dir1", "dir4", "file1.txt"]));
        Assert.False(matcher.IsMatch(["dir4"]));
        Assert.False(matcher.IsMatch(["dir4", "file1.txt"]));
    }

    [Fact]
    public void When_MatchingOnePathComponentsFileRecursive_Then_EqualOrMorePathComponentsMatch()
    {
        // arrange
        const bool isFile = true;
        const bool recursive = true;
        var matcher = new PathComponentMatcher(["file1.txt"], isFile: isFile, recursive: recursive);

        // act and assert
        Assert.True(matcher.IsMatch(["file1.txt"]));
        Assert.True(matcher.IsMatch(["dir1", "file1.txt"]));
        Assert.True(matcher.IsMatch(["dir1", "dir3", "file1.txt"]));
        Assert.False(matcher.IsMatch(["file2.txt"]));
        Assert.False(matcher.IsMatch(["dir1", "file2.txt"]));
    }
    
    [Fact]
    public void When_MatchingWithoutAnyPathComponents_Then_EqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcher([]);
        
        Assert.True(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "dir3" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir3" }));
    }

    [Fact]
    public void When_MatchingOnePathComponents_Then_EqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcher(new []{ "dir1" });
        
        Assert.True(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "dir3" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir3" }));
        Assert.False(matcher.IsMatch(new[] { "dir2" }));
    }
    
    [Fact]
    public void When_MatchingTwoPathComponents_Then_EqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcher(new []{ "dir1", "dir2" });
        
        Assert.False(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "dir3" }));
        Assert.False(matcher.IsMatch(new[] { "dir1", "dir3" }));
    }
    
    [Fact]
    public void When_MatchingOnePathComponentWithPatternAndRecursive_Then_EqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcher(new[] { "*.png" }, recursive: true);
        
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
        var matcher = new PathComponentMatcher(new[] { "dir1", "*.txt" });

        Assert.False(matcher.IsMatch(new[] { "file1.txt" }));
        Assert.False(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "file1.txt" }));
        Assert.False(matcher.IsMatch(new[] { "dir1", "image.gif" }));
        Assert.False(matcher.IsMatch(new[] { "dir1", "dir2", "file2.txt" }));
    }

    [Fact]
    public void When_MatchingOnePathComponentsWithAnyPattern_Then_EqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcher(new[] { "*" }, recursive: true);

        Assert.True(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir3" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "file1.txt" }));
        Assert.True(matcher.IsMatch(new[] { "dir2" }));
    }

    [Fact]
    public void When_MatchingTwoPathComponentsWithAnyPattern_Then_EqualOrMorePathComponentsMatch()
    {
        var matcher = new PathComponentMatcher(new []{ "dir1", "*" });
        
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
        var matcher = new PathComponentMatcher(new []{ "dir1", "dir4", "*" });
        
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
        var matcher = new PathComponentMatcher(new []{ "dir1", "*.png" }, recursive: true);
        
        Assert.False(matcher.IsMatch(new[] { "dir1" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "file1.png" }));
        Assert.False(matcher.IsMatch(new[] { "dir1", "dir2", "file2.gif" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "dir2", "file3.png" }));
        Assert.True(matcher.IsMatch(new[] { "dir1", "file4.png" }));
        Assert.False(matcher.IsMatch(new[] { "file5.png" }));
        Assert.False(matcher.IsMatch(new[] { "dir2", "file6.png" }));
    }
}