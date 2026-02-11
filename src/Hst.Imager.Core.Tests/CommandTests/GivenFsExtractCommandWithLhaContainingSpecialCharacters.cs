using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.UaeMetadatas;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenFsExtractCommandWithLhaContainingSpecialCharacters
{
    [Fact]
    public async Task When_ExtractingLhaWithUaeFsDbMetadata_Then_LhaIsExtractedWithUaeMetadata()
    {
        // arrange - paths
        var lhaPath = Path.Combine("TestData", "Lha", "special_chars.lha");
        var srcPath = $"{Guid.NewGuid()}.lha";
        var destPath = $"extract-{Guid.NewGuid()}";
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;
        
        try
        {
            // arrange - copy lha to src path
            File.Copy(lhaPath, srcPath, true);

            // arrange - create dest directory
            Directory.CreateDirectory(destPath);
            
            // arrange - create test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                [], srcPath, destPath, true, true, true, true,
                true, uaeMetadata);

            // act - execute fs extract command
            await fsExtractCommand.Execute(CancellationToken.None);

            // assert - directory was extracted
            var expectedDirs = new[]
            {
                Path.Combine(destPath, "__uae___dir1_"),
                Path.Combine(destPath, "dir2")
            };
            var actualDirs = Directory.GetDirectories(destPath, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x).ToArray();
            Assert.Equal(expectedDirs, actualDirs);
            
            // assert - files were extracted
            var expectedFiles = new[]
            {
                Path.Combine(destPath, "__uae___file1_"),
                Path.Combine(destPath, "__uae___file2_"),
                Path.Combine(destPath, "__uae___file4_"),
                Path.Combine(destPath, "__uae___file5__"),
                Path.Combine(destPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName),
                Path.Combine(destPath, "file3"),
                Path.Combine(destPath, "file6.t"),
                Path.Combine(destPath, "file7..t")
            };
            var actualFiles = Directory.GetFiles(destPath, "*.*", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x).ToArray();
            Assert.Equal(expectedFiles, actualFiles);
            
            // assert - uaefsdb metadata contains 6 nodes
            var uaeFsDbPath = Path.Combine(destPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
            var uaeMetadataNodes = (await UaeMetadataTestHelper.ReadUaeFsDbNodes(uaeFsDbPath)).ToList();
            Assert.Equal(6, uaeMetadataNodes.Count);
            
            // assert - uaefsdb metadata contains 2 directories and 4 files
            Assert.Equal("dir1*", uaeMetadataNodes.FirstOrDefault(x => x.AmigaName == "dir1*")?.AmigaName);
            Assert.Equal("dir2", uaeMetadataNodes.FirstOrDefault(x => x.AmigaName == "dir2")?.AmigaName);
            Assert.Equal("file1*", uaeMetadataNodes.FirstOrDefault(x => x.AmigaName == "file1*")?.AmigaName);
            Assert.Equal("file2<", uaeMetadataNodes.FirstOrDefault(x => x.AmigaName == "file2<")?.AmigaName);
            Assert.Equal("file4.", uaeMetadataNodes.FirstOrDefault(x => x.AmigaName == "file4.")?.AmigaName);
            Assert.Equal("file5..", uaeMetadataNodes.FirstOrDefault(x => x.AmigaName == "file5..")?.AmigaName);

            // assert - uaefsdb metadata contains script protection bit for dir2
            var dir2Mode = uaeMetadataNodes.FirstOrDefault(x => x.AmigaName == "dir2")?.Mode;
            Assert.NotNull(dir2Mode);
            Assert.Equal(ProtectionBits.Script, (ProtectionBits)dir2Mode);
            
            // assert - uaefsdb metadata contains script protection bit for file1
            var file1Mode = uaeMetadataNodes.FirstOrDefault(x => x.AmigaName == "file1*")?.Mode;
            Assert.NotNull(file1Mode);
            Assert.Equal(ProtectionBits.Script, (ProtectionBits)file1Mode);
            
            // assert - uaefsdb metadata contains comment for file2
            var file2Comment = uaeMetadataNodes.FirstOrDefault(x => x.AmigaName == "file2<")?.Comment;
            Assert.NotNull(file2Comment);
            Assert.Equal("comment on file2", file2Comment);
        }
        finally
        {
            TestHelper.DeletePaths(srcPath, destPath);
        }
    }
    
    [Fact]
    public async Task When_ExtractingSingleFileFromLhaWithUaeFsDbMetadata_Then_FileIsExtractedWithUaeMetadata()
    {
        // arrange - paths
        var lhaPath = Path.Combine("TestData", "Lha", "special_chars.lha");
        var srcPath = $"{Guid.NewGuid()}.lha";
        var srcFilePath = Path.Combine(srcPath, "file1*");
        var destPath = $"extract-{Guid.NewGuid()}";
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;
        
        try
        {
            // arrange - copy lha to src path
            File.Copy(lhaPath, srcPath, true);

            // arrange - create dest directory
            Directory.CreateDirectory(destPath);
            
            // arrange - create test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                [], srcFilePath, destPath, true, true, true, true,
                true, uaeMetadata);

            // act - execute fs extract command
            await fsExtractCommand.Execute(CancellationToken.None);

            // assert - no directories was extracted
            var actualDirs = Directory.GetDirectories(destPath, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x).ToArray();
            Assert.Empty(actualDirs);
            
            // assert - file was extracted
            var expectedFiles = new[]
            {
                Path.Combine(destPath, "__uae___file1_"),
                Path.Combine(destPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName),
            };
            var actualFiles = Directory.GetFiles(destPath, "*.*", SearchOption.TopDirectoryOnly)
                .OrderBy(x => x).ToArray();
            Assert.Equal(expectedFiles, actualFiles);   
            
            // assert - uaefsdb metadata contains 1 node
            var uaeFsDbPath = Path.Combine(destPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
            var uaeMetadataNodes = (await UaeMetadataTestHelper.ReadUaeFsDbNodes(uaeFsDbPath)).ToList();
            Assert.Single(uaeMetadataNodes);
            
            // assert - uaefsdb metadata contains 1 file
            Assert.Equal("file1*", uaeMetadataNodes.FirstOrDefault(x => x.AmigaName == "file1*")?.AmigaName);

            // assert - uaefsdb metadata contains script protection bit for file1
            var file1Mode = uaeMetadataNodes.FirstOrDefault(x => x.AmigaName == "file1*")?.Mode;
            Assert.NotNull(file1Mode);
            Assert.Equal(ProtectionBits.Script, (ProtectionBits)file1Mode);
        }
        finally
        {
            TestHelper.DeletePaths(srcPath, destPath);
        }
    }
}