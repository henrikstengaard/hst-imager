using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.UaeFsDbs;
using Hst.Amiga.FileSystems;
using Hst.Core.Extensions;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.UaeMetadatas;
using Xunit;

namespace Hst.Imager.Core.Tests.UaeMetadataHelperTests;

public class GivenUaeMetadataHelperWithLocalDirectory
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_GetUaeMetadataFromLocalDirectoryWithoutUaeMetadata_Then_PathsAreEqual(bool isFullPath)
    {
        // arrange - paths
        var localPath = $"local_{Guid.NewGuid()}";
        if (isFullPath)
        {
            localPath = Path.GetFullPath(localPath);
        }
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - create local directory
            Directory.CreateDirectory(localPath);

            // arrange - create app cache
            using var appCache = new TestAppCache();
            
            // arrange - create uae metadata helper
            var uaeMetadataHelper = new UaeMetadataHelper(appCache);
            
            // arrange - get path components for local path
            var localPathComponents = PathHelper.Split(localPath);

            // act - get uae metadata entry from path components
            var uaeMetadataEntry =
                await uaeMetadataHelper.GetUaeMetadataEntry(uaeMetadata, localPathComponents);
            
            // assert - uae metadata entry is not null
            Assert.NotNull(uaeMetadataEntry);
            
            // assert - uae metadata entry path components are equal to local path components
            Assert.Equal(localPathComponents, uaeMetadataEntry.NormalPathComponents);
            
            // assert - uae metadata entry uae path components are equal to local path components
            Assert.Equal(localPathComponents, uaeMetadataEntry.UaePathComponents);
        }
        finally
        {
            TestHelper.DeletePaths(localPath);
        }
    }

    [Fact]
    public async Task When_GetUaeMetadataFromDirectoryWithUaeFsDbMetadata_Then_PathsAreEqual()
    {
        // arrange - paths
        var localPath = $"local_{Guid.NewGuid()}";
        var file1Name = "file1ß";
        var file1Path = Path.Combine(localPath, file1Name);
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - create local directory
            Directory.CreateDirectory(localPath);

            // arrange - create safe and normal names for file1
            var file1SafeName = UaeFsDbNodeHelper.MakeSafeFilename(file1Name);
            var file1NormalName = UaeFsDbNodeHelper.CreateUniqueNormalName(localPath, file1SafeName);

            // arrange - create file1 in local directory
            await File.WriteAllBytesAsync(Path.Combine(localPath, file1NormalName), []);

            // arrange - create uaefsdb version 1 nodes for file1 in local directory
            var uaeFsDbPath = Path.Combine(localPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
            using (var uaeFsDbStream = File.OpenWrite(uaeFsDbPath))
            {
                await uaeFsDbStream.WriteBytes(UaeFsDbWriter.Build(new UaeFsDbNode
                {
                    Version = UaeFsDbNode.NodeVersion.Version1,
                    Valid = 1,
                    AmigaName = file1Name,
                    NormalName = file1NormalName,
                    Mode = ProtectionBitsConverter.ToProtectionValue(ProtectionBits.Read | ProtectionBits.Script),
                    Comment = $"{file1Name} comment"
                }));
            }
            
            // arrange - create app cache
            using var appCache = new TestAppCache();
            
            // arrange - create uae metadata helper
            var uaeMetadataHelper = new UaeMetadataHelper(appCache);
            
            // arrange - get path components for local path
            var file1PathComponents = PathHelper.Split(file1Path);

            // act - get uae metadata entry from path components
            var uaeMetadataEntry = await uaeMetadataHelper.GetUaeMetadataEntry(uaeMetadata,
                file1PathComponents);
            
            // assert - uae metadata entry is not null
            Assert.NotNull(uaeMetadataEntry);
            
            // assert - uae metadata entry path components are equal to local path components
            var expectedLocalPathComponents = PathHelper.Split(localPath)
                .Concat([file1NormalName]).ToArray();
            Assert.Equal(expectedLocalPathComponents, uaeMetadataEntry.NormalPathComponents);
            
            // assert - uae metadata entry uae path components are equal to local path components
            Assert.Equal(file1PathComponents, uaeMetadataEntry.UaePathComponents);
        }
        finally
        {
            TestHelper.DeletePaths(localPath);
        }
    }

    [Fact]
    public async Task When_GetUaeMetadataFromSubDirectoryWithUaeFsDbMetadata_Then_PathsAreEqual()
    {
        // arrange - paths
        var localPath = $"local_{Guid.NewGuid()}";
        var dir1Name = "dir1ß";
        var file1Name = "file1ß";
        var file1Path = Path.Combine(localPath, dir1Name, file1Name);
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - create local directory
            Directory.CreateDirectory(localPath);

            // arrange - create safe and normal names for dir1
            var dir1SafeName = UaeFsDbNodeHelper.MakeSafeFilename(dir1Name);
            var dir1NormalName = UaeFsDbNodeHelper.CreateUniqueNormalName(localPath, dir1SafeName);
            
            // arrange - create dir1 directory
            var dir1Path = Path.Combine(localPath, dir1NormalName);
            Directory.CreateDirectory(dir1Path);

            // arrange - create uaefsdb version 1 nodes for dir1 in local directory
            var uaeFsDbPath = Path.Combine(localPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
            using (var uaeFsDbStream = File.OpenWrite(uaeFsDbPath))
            {
                await uaeFsDbStream.WriteBytes(UaeFsDbWriter.Build(new UaeFsDbNode
                {
                    Version = UaeFsDbNode.NodeVersion.Version1,
                    Valid = 1,
                    AmigaName = dir1Name,
                    NormalName = dir1NormalName,
                    Mode = ProtectionBitsConverter.ToProtectionValue(ProtectionBits.Read | ProtectionBits.Script),
                    Comment = $"{dir1Name} comment"
                }));
            }
            
            // arrange - create safe and normal names for file1
            var file1SafeName = UaeFsDbNodeHelper.MakeSafeFilename(file1Name);
            var file1NormalName = UaeFsDbNodeHelper.CreateUniqueNormalName(dir1Name, file1SafeName);

            // arrange - create file1 in dir1 directory
            await File.WriteAllBytesAsync(Path.Combine(dir1Path, file1NormalName), []);

            // arrange - create uaefsdb version 1 nodes for file1 in local directory
            uaeFsDbPath = Path.Combine(dir1Path, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
            using (var uaeFsDbStream = File.OpenWrite(uaeFsDbPath))
            {
                await uaeFsDbStream.WriteBytes(UaeFsDbWriter.Build(new UaeFsDbNode
                {
                    Version = UaeFsDbNode.NodeVersion.Version1,
                    Valid = 1,
                    AmigaName = file1Name,
                    NormalName = file1NormalName,
                    Mode = ProtectionBitsConverter.ToProtectionValue(ProtectionBits.Read | ProtectionBits.Script),
                    Comment = $"{file1Name} comment"
                }));
            }
            
            // arrange - create app cache
            using var appCache = new TestAppCache();
            
            // arrange - create uae metadata helper
            var uaeMetadataHelper = new UaeMetadataHelper(appCache);
            
            // arrange - get path components for local path
            var file1PathComponents = PathHelper.Split(file1Path);

            // act - get uae metadata entry from path components
            var uaeMetadataEntry = await uaeMetadataHelper.GetUaeMetadataEntry(uaeMetadata,
                file1PathComponents);
            
            // assert - uae metadata entry is not null
            Assert.NotNull(uaeMetadataEntry);
            
            // assert - uae metadata entry path components are equal to local path components
            var expectedLocalPathComponents = PathHelper.Split(localPath)
                .Concat([dir1NormalName, file1NormalName]).ToArray();
            Assert.Equal(expectedLocalPathComponents, uaeMetadataEntry.NormalPathComponents);
            
            // assert - uae metadata entry uae path components are equal to local path components
            Assert.Equal(file1PathComponents, uaeMetadataEntry.UaePathComponents);
        }
        finally
        {
            TestHelper.DeletePaths(localPath);
        }
    }

    [Fact]
    public async Task When_GetUaeMetadataNodesIndexedByAmigaNameWithDuplicates_Then_NodeIsOverwritten()
    {
        // arrange - paths
        var localPath = $"local_{Guid.NewGuid()}";
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - create local directory
            Directory.CreateDirectory(localPath);
            
            // arrange - create uaefsdb version 1 nodes in local directory with duplicate amiga names
            var uaeFsDbPath = Path.Combine(localPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
            using (var uaeFsDbStream = File.OpenWrite(uaeFsDbPath))
            {
                await uaeFsDbStream.WriteBytes(UaeFsDbWriter.Build(new UaeFsDbNode
                {
                    Version = UaeFsDbNode.NodeVersion.Version1,
                    Valid = 1,
                    AmigaName = "file1",
                    NormalName = "file1 duplicate1",
                    Mode = ProtectionBitsConverter.ToProtectionValue(ProtectionBits.Read),
                    Comment = "file1"
                }));
                await uaeFsDbStream.WriteBytes(UaeFsDbWriter.Build(new UaeFsDbNode
                {
                    Version = UaeFsDbNode.NodeVersion.Version1,
                    Valid = 1,
                    AmigaName = "file1",
                    NormalName = "file1 duplicate2",
                    Mode = ProtectionBitsConverter.ToProtectionValue(ProtectionBits.Read),
                    Comment = "file1"
                }));
            }

            // arrange - create app cache
            using var appCache = new TestAppCache();
            
            // arrange - create uae metadata helper
            var uaeMetadataHelper = new UaeMetadataHelper(appCache);

            // act - get uae metadata nodes indexed by amiga name
            var nodesIndexedByAmigaName = await uaeMetadataHelper.GetUaeMetadataNodesIndexedByAmigaName(uaeMetadata, localPath);
            
            // assert - nodes indexed by amiga name contains file 1 duplicate 2
            Assert.Single(nodesIndexedByAmigaName);
            Assert.Equal("file1 duplicate2", nodesIndexedByAmigaName["file1"].NormalName);
        }
        finally
        {
            TestHelper.DeletePaths(localPath);
        }
    }

    [Fact]
    public async Task When_GetUaeMetadataNodesIndexedByNormalNameWithDuplicates_Then_NodeIsOverwritten()
    {
        // arrange - paths
        var localPath = $"local_{Guid.NewGuid()}";
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - create local directory
            Directory.CreateDirectory(localPath);
            
            // arrange - create uaefsdb version 1 nodes in local directory with duplicate normal names
            var uaeFsDbPath = Path.Combine(localPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
            using (var uaeFsDbStream = File.OpenWrite(uaeFsDbPath))
            {
                await uaeFsDbStream.WriteBytes(UaeFsDbWriter.Build(new UaeFsDbNode
                {
                    Version = UaeFsDbNode.NodeVersion.Version1,
                    Valid = 1,
                    AmigaName = "file1 duplicate1",
                    NormalName = "file1",
                    Mode = ProtectionBitsConverter.ToProtectionValue(ProtectionBits.Read),
                    Comment = "file1"
                }));
                await uaeFsDbStream.WriteBytes(UaeFsDbWriter.Build(new UaeFsDbNode
                {
                    Version = UaeFsDbNode.NodeVersion.Version1,
                    Valid = 1,
                    AmigaName = "file1 duplicate2",
                    NormalName = "file1",
                    Mode = ProtectionBitsConverter.ToProtectionValue(ProtectionBits.Read),
                    Comment = "file1"
                }));
            }

            // arrange - create app cache
            using var appCache = new TestAppCache();
            
            // arrange - create uae metadata helper
            var uaeMetadataHelper = new UaeMetadataHelper(appCache);

            // act - get uae metadata nodes indexed by normal name
            var nodesIndexedByNormalName = await uaeMetadataHelper.GetUaeMetadataNodesIndexedByNormalName(uaeMetadata, localPath);
            
            // assert - nodes indexed by amiga name contains file 1 duplicate 2
            Assert.Single(nodesIndexedByNormalName);
            Assert.Equal("file1 duplicate2", nodesIndexedByNormalName["file1"].AmigaName);
        }
        finally
        {
            TestHelper.DeletePaths(localPath);
        }
    }
}