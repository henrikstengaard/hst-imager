using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.UaeFsDbs;
using Hst.Amiga.FileSystems;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.UaeMetadatas;
using Xunit;
using Entry = Hst.Imager.Core.Models.FileSystems.Entry;
using EntryType = Hst.Imager.Core.Models.FileSystems.EntryType;

namespace Hst.Imager.Core.Tests.CommandTests.EntryWriterTests;

public class GivenDirectoryEntryWriterWithUaeFsDbMetadata
{
    [Fact]
    public async Task When_InitializingWriterInDirectory_Then_WriterCreatesPathWithUaeMetadata()
    {
        // arrange - paths
        var localPath = Guid.NewGuid().ToString();
        var initPath = Path.Combine(localPath, "dir1ß");
        const bool createDirectory = true;

        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;
        
        // arrange - test app cache
        using var appCache = new TestAppCache();
        
        try
        {
            // arrange - directory entry writer
            var directoryEntryWriter = new DirectoryEntryWriter(initPath, false, createDirectory,
                false, appCache);
            directoryEntryWriter.UaeMetadata = uaeMetadata;

            // act - initialize writer
            var initializeResult = await directoryEntryWriter.Initialize();

            // assert - writer was initialized successfully
            Assert.True(initializeResult.IsSuccess);

            // assert - local dir contains 1 directory
            var actualDirs = Directory.GetDirectories(localPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(actualDirs);

            // assert - local dir contains directory with normalized name
            var expectedDirs = new[]
            {
                Path.Combine(localPath, "__uae___dir1_")
            };
            Assert.Equal(expectedDirs, actualDirs.OrderBy(x => x).ToArray());
            
            // assert - local dir contains 1 file
            var actualFiles = Directory.GetFiles(localPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(actualFiles);

            // assert - local dir contains uaefsdb metadata file
            var expectedFiles = new[]
            {
                Path.Combine(localPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName)
            };
            Assert.Equal(expectedFiles, actualFiles.OrderBy(x => x).ToArray());
            
            // assert - dir1 contains 0 dirs
            var dir1Path = Path.Combine(localPath, "__uae___dir1_");
            actualDirs = Directory.GetDirectories(dir1Path, "*", SearchOption.TopDirectoryOnly);
            Assert.Empty(actualDirs);

            // assert - dir1 contains 0 files
            var actualFilesInDir1 = Directory.GetFiles(dir1Path, "*", SearchOption.TopDirectoryOnly);
            Assert.Empty(actualFilesInDir1);
        }
        finally
        {
            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, true);
            }
        }
    }

    [Fact]
    public async Task When_InitializingWriterInSubdirectory_Then_WriterCreatesPathWithUaeMetadata()
    {
        // arrange - paths
        var localPath = Guid.NewGuid().ToString();
        var initPath = Path.Combine(localPath, "dir1ß", "dir2ß");
        const bool createDirectory = true;

        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;
        
        // arrange - test app cache
        using var appCache = new TestAppCache();
        
        try
        {
            // create local directory
            Directory.CreateDirectory(localPath);
            
            // arrange - create safe and normal names for file1
            var dir1SafeName = UaeFsDbNodeHelper.MakeSafeFilename("dir1ß");
            var dir1NormalName = UaeFsDbNodeHelper.CreateUniqueNormalName(localPath, dir1SafeName);

            // create dir1 subdirectory
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
                    AmigaName = "dir1ß",
                    NormalName = dir1NormalName,
                    Mode = ProtectionBitsConverter.ToProtectionValue(ProtectionBits.Read | ProtectionBits.Script),
                    Comment = "dir1ß comment"
                }));
            }
            
            // arrange - directory entry writer
            var directoryEntryWriter = new DirectoryEntryWriter(initPath, false, createDirectory,
                false, appCache);
            directoryEntryWriter.UaeMetadata = uaeMetadata;

            // act - initialize writer
            var initializeResult = await directoryEntryWriter.Initialize();

            // assert - writer was initialized successfully
            Assert.True(initializeResult.IsSuccess);

            // assert - local dir contains 1 directory
            var actualDirs = Directory.GetDirectories(localPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(actualDirs);

            // assert - local dir contains directory with normalized name
            var expectedDirs = new[]
            {
                Path.Combine(localPath, "__uae___dir1_")
            };
            Assert.Equal(expectedDirs, actualDirs.OrderBy(x => x).ToArray());
            
            // assert - local dir contains 1 file
            var actualFiles = Directory.GetFiles(localPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(actualFiles);

            // assert - local dir contains uaefsdb metadata file
            var expectedFiles = new[]
            {
                Path.Combine(localPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName)
            };
            Assert.Equal(expectedFiles, actualFiles.OrderBy(x => x).ToArray());
            
            // assert - dir1 contains 1 directory
            actualDirs = Directory.GetDirectories(dir1Path, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(actualDirs);

            // assert - dir1 contains dir2 with normalized name
            expectedDirs =
            [
                Path.Combine(dir1Path, "__uae___dir2_")
            ];
            Assert.Equal(expectedDirs, actualDirs.OrderBy(x => x).ToArray());
            
            // assert - dir1 contains 1 file
            actualFiles = Directory.GetFiles(dir1Path, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(actualFiles);

            // assert - dir1 contains uaefsdb metadata file
            expectedFiles =
            [
                Path.Combine(dir1Path, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName)
            ];
            Assert.Equal(expectedFiles, actualFiles.OrderBy(x => x).ToArray());
            
            // assert - dir2 contains 0 dirs
            var dir2Path = Path.Combine(dir1Path, "__uae___dir2_");
            actualDirs = Directory.GetDirectories(dir2Path, "*", SearchOption.TopDirectoryOnly);
            Assert.Empty(actualDirs);
            
            // assert - dir2 contains 0 files
            actualFiles = Directory.GetFiles(dir2Path, "*", SearchOption.TopDirectoryOnly);
            Assert.Empty(actualFiles);
        }
        finally
        {
            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, true);
            }
        }
    }
    
    [Fact]
    public async Task When_CreatingFileWithUaeFsDbMetadata_Then_FileIsCreatedWithUaeMetadata()
    {
        // arrange - paths
        var localPath = Guid.NewGuid().ToString();

        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;
        
        // arrange - test app cache
        using var appCache = new TestAppCache();
        
        try
        {
            // arrange - create test files
            Directory.CreateDirectory(localPath);

            // arrange - directory entry writer
            var directoryEntryWriter = new DirectoryEntryWriter(localPath, false, false,
                false, appCache);
            directoryEntryWriter.UaeMetadata = uaeMetadata;
            var initializeResult = await directoryEntryWriter.Initialize();
            Assert.True(initializeResult.IsSuccess);

            // arrange - entry to create
            var entry = new Entry
            {
                Name = "file1ß",
                Type = EntryType.File,
                Size = 0,
                Date = DateTime.Now,
                RawPath = "?",
                RelativePathComponents = ["file1ß"],
                FullPathComponents = [localPath, "file1ß"]
            };
            string[] entryPathComponents = ["file1ß"]; 
            using var entryStream = new MemoryStream();
            
            // act - create file
            var result = await directoryEntryWriter.CreateFile(entry, entryPathComponents, entryStream, false,
                true);
            
            // assert - file was created successfully
            Assert.True(result.IsSuccess);
            
            // assert - local dir contains 2 files
            var actualFiles = Directory.GetFiles(localPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Equal(2, actualFiles.Length);

            // assert - local dir contains file with normalized name and uaefsdb metadata file
            var expectedFiles = new[]
            {
                Path.Combine(localPath, "__uae___file1_"),
                Path.Combine(localPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName)
            };
            Assert.Equal(expectedFiles, actualFiles.OrderBy(x => x).ToArray());
            
            // assert - local dir contains 0 dirs
            var actualDirs = Directory.GetDirectories(localPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Empty(actualDirs);
        }
        finally
        {
            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, true);
            }
        }
    }

    [Fact]
    public async Task When_CreatingFileInDirectoryWithUaeFsDbMetadata_Then_FileIsCreatedWithUaeMetadata()
    {
        // arrange - paths
        var localPath = Guid.NewGuid().ToString();
        const bool createDirectory = true;
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;
        
        // arrange - test app cache
        using var appCache = new TestAppCache();
        
        try
        {
            // arrange - create test files
            Directory.CreateDirectory(localPath);

            // arrange - directory entry writer
            var directoryEntryWriter = new DirectoryEntryWriter(localPath, false, createDirectory,
                false, appCache);
            directoryEntryWriter.UaeMetadata = uaeMetadata;
            var initializeResult = await directoryEntryWriter.Initialize();
            Assert.True(initializeResult.IsSuccess);

            // arrange - entry to create
            var entry = new Entry
            {
                Name = "file1ß",
                Type = EntryType.File,
                Size = 0,
                Date = DateTime.Now,
                RawPath = "?",
                RelativePathComponents = ["dir1ß", "file1ß"],
                FullPathComponents = [localPath, "dir1ß", "file1ß"]
            };
            string[] entryPathComponents = ["dir1ß", "file1ß"]; 
            using var entryStream = new MemoryStream();
            
            // act - create file
            var result = await directoryEntryWriter.CreateFile(entry, entryPathComponents, entryStream, false,
                true);
            
            // assert - file was created successfully
            Assert.True(result.IsSuccess);

            // assert - local dir contains 1 directory
            var actualDirs = Directory.GetDirectories(localPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(actualDirs);
            
            // assert - local dir contains dir1 with normalized name
            var expectedDirs = new[]
            {
                Path.Combine(localPath, "__uae___dir1_")
            };
            Assert.Equal(expectedDirs, actualDirs.OrderBy(x => x).ToArray());

            // assert - local dir contains 1 file
            var actualFiles = Directory.GetFiles(localPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(actualFiles);
            
            // assert - local dir contains uaefsdb metadata file
            var expectedFiles = new[]
            {
                Path.Combine(localPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName)
            };
            Assert.Equal(expectedFiles, actualFiles.OrderBy(x => x).ToArray());

            // assert - dir1 contains 2 files
            var dir1Path = Path.Combine(localPath, "__uae___dir1_");
            actualFiles = Directory.GetFiles(dir1Path, "*", SearchOption.TopDirectoryOnly);
            Assert.Equal(2, actualFiles.Length);

            // assert - dir1 contains file with normalized name and uaefsdb metadata file
            expectedFiles =
            [
                Path.Combine(dir1Path, "__uae___file1_"),
                Path.Combine(dir1Path, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName)
            ];
            Assert.Equal(expectedFiles, actualFiles.OrderBy(x => x).ToArray());
            
            // assert - dir1 contains 0 dirs
            actualDirs = Directory.GetDirectories(dir1Path, "*", SearchOption.TopDirectoryOnly);
            Assert.Empty(actualDirs);
        }
        finally
        {
            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, true);
            }
        }
    }
    
    [Fact]
    public async Task When_CreatingDirectoryWithUaeFsDbMetadata_Then_DirectoryIsCreatedWithMetadata()
    {
        // arrange - paths
        var localPath = Guid.NewGuid().ToString();

        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;
        
        // arrange - test app cache
        using var appCache = new TestAppCache();
        
        try
        {
            // arrange - create test files
            Directory.CreateDirectory(localPath);

            // arrange - directory entry writer
            var directoryEntryWriter = new DirectoryEntryWriter(localPath, false, false,
                false, appCache);
            directoryEntryWriter.UaeMetadata = uaeMetadata;
            var initializeResult = await directoryEntryWriter.Initialize();
            Assert.True(initializeResult.IsSuccess);

            // arrange - entry to create
            var entry = new Entry
            {
                Name = "dir1ß",
                Type = EntryType.Dir,
                Size = 0,
                Date = DateTime.Now,
                RawPath = "dir1ß",
                RelativePathComponents = ["dir1ß"],
                FullPathComponents = [localPath, "dir1ß"]
            };
            string[] entryPathComponents = ["dir1ß"]; 
            using var entryStream = new MemoryStream();
            
            // act - create directory
            var result = await directoryEntryWriter.CreateDirectory(entry, entryPathComponents, false,
                false);
            
            // assert - directory was created successfully
            Assert.True(result.IsSuccess);

            // assert - local dir contains 1 directory
            var actualDirs = Directory.GetDirectories(localPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(actualDirs);

            // assert - local dir contains dir1 with normalized name
            var expectedDirs = new[]
            {
                Path.Combine(localPath, "__uae___dir1_")
            };
            Assert.Equal(expectedDirs, actualDirs.OrderBy(x => x).ToArray());
            
            // assert - local dir contains 1 file
            var actualFiles = Directory.GetFiles(localPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(actualFiles);

            // assert - local dir contains uaefsdb metadata file
            var expectedFiles = new[]
            {
                Path.Combine(localPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName)
            };
            Assert.Equal(expectedFiles, actualFiles.OrderBy(x => x).ToArray());
            
            // assert - dir1 contains 0 dirs
            var dir1Path = Path.Combine(localPath, "__uae___dir1_");
            actualDirs = Directory.GetDirectories(dir1Path, "*", SearchOption.TopDirectoryOnly);
            Assert.Empty(actualDirs);
            
            // assert - dir1 contains 0 files
            actualFiles = Directory.GetDirectories(dir1Path, "*", SearchOption.TopDirectoryOnly);
            Assert.Empty(actualFiles);
        }
        finally
        {
            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, true);
            }
        }
    }
    
    [Fact]
    public async Task When_CreatingSubdirectoryWithUaeFsDbMetadata_Then_DirectoryIsCreatedWithMetadata()
    {
        // arrange - paths
        var localPath = Guid.NewGuid().ToString();

        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;
        
        // arrange - test app cache
        using var appCache = new TestAppCache();
        
        try
        {
            // arrange - create test files
            Directory.CreateDirectory(localPath);

            // arrange - directory entry writer
            var directoryEntryWriter = new DirectoryEntryWriter(localPath, false, false,
                false, appCache);
            directoryEntryWriter.UaeMetadata = uaeMetadata;
            var initializeResult = await directoryEntryWriter.Initialize();
            Assert.True(initializeResult.IsSuccess);

            // arrange - entry to create
            var entry = new Entry
            {
                Name = "dir2ß",
                Type = EntryType.Dir,
                Size = 0,
                Date = DateTime.Now,
                RawPath = "dir2ß",
                RelativePathComponents = ["dir1ß", "dir2ß"],
                FullPathComponents = [localPath, "dir1ß", "dir2ß"]
            };
            string[] entryPathComponents = ["dir1ß", "dir2ß"]; 
            using var entryStream = new MemoryStream();
            
            // act - create directory
            var result = await directoryEntryWriter.CreateDirectory(entry, entryPathComponents, false,
                false);
            
            // assert - directory was created successfully
            Assert.True(result.IsSuccess);

            // assert - local dir contains 1 directory
            var actualDirs = Directory.GetDirectories(localPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(actualDirs);

            // assert - local dir contains dir1 with normalized name
            var expectedDirs = new[]
            {
                Path.Combine(localPath, "__uae___dir1_")
            };
            Assert.Equal(expectedDirs, actualDirs.OrderBy(x => x).ToArray());
            
            // assert - local dir contains 1 file
            var actualFiles = Directory.GetFiles(localPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(actualFiles);

            // assert - local dir contains uaefsdb metadata file
            var expectedFiles = new[]
            {
                Path.Combine(localPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName)
            };
            Assert.Equal(expectedFiles, actualFiles.OrderBy(x => x).ToArray());
            
            // assert - dir1 contains 1 directory
            var dir1Path = Path.Combine(localPath, "__uae___dir1_");
            actualDirs = Directory.GetDirectories(dir1Path, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(actualDirs);
            
            // assert - dir1 contains dir2 with normalized name
            expectedDirs = new[]
            {
                Path.Combine(dir1Path, "__uae___dir2_")
            };
            Assert.Equal(expectedDirs, actualDirs.OrderBy(x => x).ToArray());

            // assert - dir1 contains 1 file
            actualFiles = Directory.GetFiles(dir1Path, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(actualFiles);

            // assert - dir1 contains uaefsdb metadata file
            expectedFiles =
            [
                Path.Combine(dir1Path, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName)
            ];
            Assert.Equal(expectedFiles, actualFiles.OrderBy(x => x).ToArray());
            
            // assert - dir1 contains 0 dirs
            actualDirs = Directory.GetDirectories(dir1Path, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(actualDirs);
            
            // assert - dir2 contains 0 dirs
            var dir2Path = Path.Combine(dir1Path, "__uae___dir2_");
            actualDirs = Directory.GetDirectories(dir2Path, "*", SearchOption.TopDirectoryOnly);
            Assert.Empty(actualDirs);
            
            // assert - dir2 contains 0 files
            actualFiles = Directory.GetFiles(dir2Path, "*", SearchOption.TopDirectoryOnly);
            Assert.Empty(actualFiles);
        }
        finally
        {
            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, true);
            }
        }
    }
}