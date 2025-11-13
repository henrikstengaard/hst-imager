using System.IO;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Helpers;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenPathHelper
{
    [Theory]
    [InlineData("AUX")]
    [InlineData(@".\AUX")]
    [InlineData(@"Storage3.2.adf\DOSDrivers\AUX")]
    [InlineData("./AUX")]
    [InlineData("~/Storage3.2.adf/DOSDrivers/AUX")]
    [InlineData("/usr/hst/Storage3.2.adf/DOSDrivers/AUX")]
    public void When_GetFullPathForRelativePath_Then_FullPathIsReturned(string path)
    {
        // act - get full path
        var fullPath = PathHelper.GetFullPath(path);
        
        // assert - full path is rooted and starts with / or windows drive
        Assert.True(fullPath.StartsWith("/") ||
                    Regexs.WindowsDriveRegex.IsMatch(fullPath));
    }
    
    [Theory]
    [InlineData(@"C:\Users\hst\Documents\Storage3.2.adf\DOSDrivers\AUX")]
    [InlineData(@"C:\AUX")]
    [InlineData(@"\\192.168.0.1\data\Storage3.2.adf\DOSDrivers\AUX")]
    [InlineData(@"/Users/hst/Storage3.2.adf/DOSDrivers/AUX")]
    [InlineData(@"/usr/hst/Storage3.2.adf/DOSDrivers/AUX")]
    public void When_GetFullPathForAbsolutePath_Then_FullPathIsReturned(string path)
    {
        // act - get full path
        var fullPath = PathHelper.GetFullPath(path);
        
        // assert - full path is equal to path, is rooted and starts with / or windows drive
        Assert.Equal(path, fullPath);
        Assert.True(fullPath.StartsWith("/") ||
                    fullPath.StartsWith(@"\\") ||
                    Regexs.WindowsDriveRegex.IsMatch(fullPath));
    }
}