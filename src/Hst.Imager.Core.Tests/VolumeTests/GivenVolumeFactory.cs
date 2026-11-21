using System;
using System.IO;
using System.Threading.Tasks;
using Hst.Imager.Core.Volumes;
using Xunit;

namespace Hst.Imager.Core.Tests.VolumeTests;

public class GivenVolumeFactory
{
    [Theory]
    [InlineData(VolumeType.Amiga)]
    [InlineData(VolumeType.DiscUtils)]
    [InlineData(VolumeType.LocalDirectory)]
    public async Task When_OpeningPathsInSameVolume_Then_SameVolumeIsReturned(VolumeType volumeType)
    {
        using var context = await VolumeTestContext.Create(volumeType);
        await context.CreateDirectoriesAndFiles();

        // act - open two paths in same volume
        var volumePath1 = await context.Open(VolumeAccess.Write, "dir1");
        var volumePath2 = await context.Open(VolumeAccess.Read, "dir1", "file2.txt");

        // assert - same volume is returned with path components in volume
        Assert.Same(volumePath1.Volume, volumePath2.Volume);
        Assert.True(volumePath1.Volume.IsSameVolume(volumePath2.Volume));
        Assert.Equal("dir1", volumePath1.PathComponents[^1]);
        Assert.Equal(["dir1", "file2.txt"], volumePath2.PathComponents[^2..]);
    }

    [Fact]
    public async Task When_OpeningAdf_Then_AmigaVolumeIsReturned()
    {
        var adfPath = $"{Guid.NewGuid()}.adf";

        try
        {
            using var testCommandHelper = new TestCommandHelper();
            await TestHelper.CreateFormattedAdfDisk(testCommandHelper, adfPath, "Workbench");
            using var appCache = new TestAppCache();
            using var factory = new VolumeFactory(testCommandHelper, [], new UaeMetadatas.UaeMetadataHelper(appCache));

            // act - open adf and directory in adf
            var adfResult = await factory.Open(adfPath, VolumeAccess.Read);
            var adfDirResult = await factory.Open(Path.Combine(adfPath, "dir1"), VolumeAccess.Read);

            // assert - amiga volume is returned for both paths
            Assert.True(adfResult.IsSuccess, adfResult.IsFaulted ? adfResult.Error.ToString() : null);
            Assert.IsType<AmigaVolume>(adfResult.Value.Volume);
            Assert.Empty(adfResult.Value.PathComponents);
            Assert.True(adfDirResult.IsSuccess);
            Assert.Same(adfResult.Value.Volume, adfDirResult.Value.Volume);
            Assert.Equal(["dir1"], adfDirResult.Value.PathComponents);
        }
        finally
        {
            TestHelper.DeletePaths(adfPath);
        }
    }

    [Fact]
    public async Task When_OpeningAdfWithoutOpenArchives_Then_LocalDirectoryVolumeIsReturned()
    {
        var adfPath = $"{Guid.NewGuid()}.adf";

        try
        {
            using var testCommandHelper = new TestCommandHelper();
            await TestHelper.CreateFormattedAdfDisk(testCommandHelper, adfPath, "Workbench");
            using var appCache = new TestAppCache();
            using var factory = new VolumeFactory(testCommandHelper, [], new UaeMetadatas.UaeMetadataHelper(appCache));

            // act - open adf as file
            var result = await factory.Open(adfPath, VolumeAccess.Read, openArchives: false);

            // assert - local directory volume is returned with adf file as last path component
            Assert.True(result.IsSuccess);
            Assert.IsType<LocalDirectoryVolume>(result.Value.Volume);
            Assert.Equal(adfPath, result.Value.PathComponents[^1]);
        }
        finally
        {
            TestHelper.DeletePaths(adfPath);
        }
    }
}
