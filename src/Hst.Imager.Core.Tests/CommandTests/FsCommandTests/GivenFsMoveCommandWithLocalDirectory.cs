using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.UaeFsDbs;
using Hst.Amiga.DataTypes.UaeMetafiles;
using Hst.Amiga.FileSystems;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.UaeMetadatas;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsMoveCommandWithLocalDirectory : FsCommandTestBase
{
    [Fact]
    public async Task When_MovingAFileOnSameMedia_Then_FileIsMoved()
    {
        // arrange - create paths
        var mediaPath = $"{Guid.NewGuid()}-local";
        const string srcFilename = "file1.txt";
        var srcPath = Path.Combine(mediaPath, srcFilename);
        const string destFilename = "file2.txt";
        var destPath = Path.Combine(mediaPath, destFilename);
        const bool forceOverwrite = false;
        const UaeMetadata uaeMetadata = UaeMetadata.None;
        
        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create media path with src file
            Directory.CreateDirectory(mediaPath);
            await File.WriteAllBytesAsync(srcPath, []);
        
            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite, uaeMetadata);
            
            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());
            
            // assert - file is moved
            var files = Directory.GetFiles(mediaPath);
            Assert.Single(files);
            Assert.DoesNotContain(files, file => Path.GetFileName(file) == srcFilename);
            Assert.Contains(files, file => Path.GetFileName(file) == destFilename);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_MovingAFileOnSameMediaAndFileDoesntExist_Then_ErrorIsReturned()
    {
        // arrange - create paths
        var mediaPath = $"{Guid.NewGuid()}-local";
        const string srcFilename = "file1.txt";
        var srcPath = Path.Combine(mediaPath, srcFilename);
        const string destFilename = "file2.txt";
        var destPath = Path.Combine(mediaPath, destFilename);
        const bool forceOverwrite = false;
        const UaeMetadata uaeMetadata = UaeMetadata.None;
        
        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create media path
            Directory.CreateDirectory(mediaPath);
        
            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite, uaeMetadata);
            
            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is faulted
            Assert.True(result.IsFaulted);
            Assert.IsType<PathNotFoundError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_MovingAFileOnSameMediaAndFileExists_Then_ErrorIsReturned()
    {
        // arrange - create paths
        var mediaPath = $"{Guid.NewGuid()}-local";
        const string srcFilename = "file1.txt";
        var srcPath = Path.Combine(mediaPath, srcFilename);
        const string destFilename = "file2.txt";
        var destPath = Path.Combine(mediaPath, destFilename);
        const bool forceOverwrite = false;
        const UaeMetadata uaeMetadata = UaeMetadata.None;

        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create media path with src file
            Directory.CreateDirectory(mediaPath);
            await File.WriteAllBytesAsync(srcPath, []);

            // arrange - create dest file
            await File.WriteAllBytesAsync(destPath, []);
        
            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite, uaeMetadata);
            
            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is faulted
            Assert.True(result.IsFaulted);
            Assert.IsType<PathExistsError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_ForceMovingAFileOnSameMediaAndFileExists_Then_FileIsMoved()
    {
        // arrange - create paths
        var mediaPath = $"{Guid.NewGuid()}-local";
        const string srcFilename = "file1.txt";
        var srcPath = Path.Combine(mediaPath, srcFilename);
        const string destFilename = "file2.txt";
        var destPath = Path.Combine(mediaPath, destFilename);
        const bool forceOverwrite = true;
        const UaeMetadata uaeMetadata = UaeMetadata.None;
        
        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create media path with src file
            Directory.CreateDirectory(mediaPath);
            await File.WriteAllBytesAsync(srcPath, []);

            // arrange - create dest file
            await File.WriteAllBytesAsync(destPath, []);
        
            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite, uaeMetadata);
            
            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());
            
            // assert - file is moved
            var files = Directory.GetFiles(mediaPath);
            Assert.Single(files);
            Assert.DoesNotContain(files, file => Path.GetFileName(file) == srcFilename);
            Assert.Contains(files, file => Path.GetFileName(file) == destFilename);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_MovingAFileToADirOnSameMedia_Then_FileIsMoved()
    {
        // arrange - create paths
        var mediaPath = $"{Guid.NewGuid()}-local";
        const string srcFilename = "file.txt";
        var srcPath = Path.Combine(mediaPath, srcFilename);
        const string destFilename = "dir";
        var destPath = Path.Combine(mediaPath, destFilename);
        const bool forceOverwrite = false;
        const UaeMetadata uaeMetadata = UaeMetadata.None;
        
        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create media path with src file
            Directory.CreateDirectory(mediaPath);
            await File.WriteAllBytesAsync(srcPath, []);
            
            // arrange - create destination directory
            Directory.CreateDirectory(destPath);
        
            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite, uaeMetadata);
            
            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());
            
            // assert - file is moved from root directory and root only contains destination directory
            var files = Directory.GetFiles(mediaPath);
            Assert.Empty(files);
            var dirs = Directory.GetDirectories(mediaPath);
            Assert.Single(dirs);
            Assert.Contains(dirs, dir => Path.GetFileName(dir) == destFilename);
            
            // assert - file is moved to destination directory
            var destFiles = Directory.GetFiles(destPath);
            Assert.Single(destFiles);
            Assert.Contains(destFiles, file => Path.GetFileName(file) == srcFilename);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_MovingANormalNameToUaeFsDbAmigaNameOnSameMedia_Then_FileIsMoved()
    {
        var mediaPath = $"{Guid.NewGuid()}-local";
        const string srcFilename = "file1.txt";
        var srcPath = Path.Combine(mediaPath, srcFilename);
        var destFilename = "file1?";
        var destPath = Path.Combine(mediaPath, destFilename);
        const bool forceOverwrite = false;
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;
        
        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create media path with file
            Directory.CreateDirectory(mediaPath);
            await File.AppendAllTextAsync(srcPath, string.Empty);
        
            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite, uaeMetadata);
            
            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());
            
            // assert - file is moved and renamed to safe filename
            var files = Directory.GetFiles(mediaPath);
            Assert.Equal(2, files.Length);
            Assert.DoesNotContain(files, file => Path.GetFileName(file) == "file1.txt");
            Assert.Contains(files, file => Path.GetFileName(file) == "__uae___file1_");
            Assert.Contains(files, file => Path.GetFileName(file) == Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_MovingAUaeFsDbAmigaNameToNormalNameOnSameMedia_Then_FileIsMoved()
    {
        // arrange - create paths
        var mediaPath = $"{Guid.NewGuid()}-local";
        const string amigaName = "file1?";
        var srcPath = Path.Combine(mediaPath, amigaName);
        const string destFilename = "file1.txt";
        var destPath = Path.Combine(mediaPath, destFilename);
        const string comment = "file1 comment";
        const bool forceOverwrite = false;
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;
        
        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create media path with file
            Directory.CreateDirectory(mediaPath);

            // arrange - create file1 and uaefsdb node
            var safeName = UaeFsDbNodeHelper.MakeSafeFilename(amigaName);
            var normalName = UaeFsDbNodeHelper.CreateUniqueFileNormalName(mediaPath, safeName);
            var srcNormalNamePath = Path.Combine(mediaPath, normalName);
            var uaeFsDbPath = Path.Combine(mediaPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
            await File.WriteAllBytesAsync(uaeFsDbPath, UaeFsDbWriter.Build(new UaeFsDbNode
            {
                AmigaName = amigaName,
                NormalName = normalName,
                Comment = comment,
                Mode = ProtectionBitsConverter.ToProtectionValue(ProtectionBits.Read | ProtectionBits.Script),
                Valid = 1,
                Version = UaeFsDbNode.NodeVersion.Version1
            }));
            await File.AppendAllTextAsync(srcNormalNamePath, string.Empty);
        
            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite, uaeMetadata);
            
            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());
            
            // assert - file is moved and renamed to safe filename
            var files = Directory.GetFiles(mediaPath);
            Assert.Equal(2, files.Length);
            Assert.DoesNotContain(files, file => Path.GetFileName(file) == Path.GetFileName(srcPath));
            Assert.Contains(files, file => Path.GetFileName(file) == destFilename);
            Assert.Contains(files, file => Path.GetFileName(file) == Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
            
            // assert - uaefsdb node is updated with new normal name
            var uaeFsDbNodes = (await UaeFsDbReader.ReadFromFile(uaeFsDbPath)).ToList();
            Assert.Single(uaeFsDbNodes);
            var uaeFsDbNode = uaeFsDbNodes.First();
            Assert.Equal(destFilename, uaeFsDbNode.AmigaName);
            Assert.Equal(destFilename, uaeFsDbNode.NormalName);
            Assert.Equal(comment, uaeFsDbNode.Comment);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_MovingANormalNameToUaeMetafileAmigaNameOnSameMedia_Then_FileIsMoved()
    {
        // arrange - create paths
        var mediaPath = $"{Guid.NewGuid()}-local";
        const string srcFilename = "file1.txt";
        var srcPath = Path.Combine(mediaPath, srcFilename);
        var destFilename = "file1?";
        var destEncodedName = UaeMetafileHelper.EncodeFilenameSpecialChars(destFilename);
        var destPath = Path.Combine(mediaPath, destFilename);
        const bool forceOverwrite = false;
        const UaeMetadata uaeMetadata = UaeMetadata.UaeMetafile;
        
        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create media path with file
            Directory.CreateDirectory(mediaPath);
            await File.AppendAllTextAsync(srcPath, string.Empty);
        
            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite, uaeMetadata);
            
            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());
            
            // assert - file is moved and renamed with uae metafile filename and uae metafile is created
            var files = Directory.GetFiles(mediaPath);
            Assert.Equal(2, files.Length);
            Assert.DoesNotContain(files, file => Path.GetFileName(file) == "file1.txt");
            Assert.Contains(files, file => Path.GetFileName(file) == string.Concat(destEncodedName,
                Amiga.DataTypes.UaeMetafiles.Constants.UaeMetafileExtension));
            Assert.Contains(files, file => Path.GetFileName(file) == destEncodedName);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_MovingAUaeMetafileAmigaNameToNormalNameOnSameMedia_Then_FileIsMoved()
    {
        // arrange - create paths
        var mediaPath = $"{Guid.NewGuid()}-local";
        var amigaName = "file?";
        var srcEncodedName = UaeMetafileHelper.EncodeFilenameSpecialChars(amigaName);
        var srcPath = Path.Combine(mediaPath, amigaName);
        var srcEncodedPath = Path.Combine(mediaPath, srcEncodedName);
        const string destFilename = "file.txt";
        var destPath = Path.Combine(mediaPath, destFilename);
        var destUaeMetafilePath = string.Concat(destPath,
            Amiga.DataTypes.UaeMetafiles.Constants.UaeMetafileExtension);
        const bool forceOverwrite = false;
        const UaeMetadata uaeMetadata = UaeMetadata.UaeMetafile;
        
        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create media path with file
            Directory.CreateDirectory(mediaPath);
            
            // arrange - create file and uae metafile
            var srcUaeMetafilePath = string.Concat(srcEncodedPath,
                Amiga.DataTypes.UaeMetafiles.Constants.UaeMetafileExtension);
            var date = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Local);
            await File.WriteAllBytesAsync(srcUaeMetafilePath, UaeMetafileWriter.Build(new UaeMetafile
            {
                Date = date,
                Comment = "file comment",
                ProtectionBits = "-s--r---"
            }));
            await File.AppendAllTextAsync(srcEncodedPath, string.Empty);
        
            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite, uaeMetadata);
            
            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());
            
            // assert - file is moved and renamed to safe filename and uae metafile is created since uae metadata contains comment and protection bits
            var files = Directory.GetFiles(mediaPath);
            Assert.Equal(2, files.Length);
            Assert.DoesNotContain(files, file => Path.GetFileName(file) == srcEncodedName);
            Assert.Contains(files, file => Path.GetFileName(file) == destFilename);
            Assert.Contains(files, file => Path.GetFileName(file) == Path.GetFileName(destUaeMetafilePath));
            
            // assert - uae metafile has comment and protection bits
            var uaeMetafile = UaeMetafileReader.Read(await File.ReadAllBytesAsync(destUaeMetafilePath));
            Assert.Equal("file comment", uaeMetafile.Comment);
            Assert.Equal("-s--r---", uaeMetafile.ProtectionBits);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_MovingADirFromAndToSameMedia_Then_DirIsMoved()
    {
        // arrange - create paths
        var srcMediaPath = $"{Guid.NewGuid()}-local";
        var srcPath = srcMediaPath;
        var destMediaPath = $"{Guid.NewGuid()}-local";
        var destPath = destMediaPath;

        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create src media directory with files
            Directory.CreateDirectory(srcMediaPath);
            await LocalTestHelper.CreateDirectoriesAndFiles(srcMediaPath);

            // arrange - create dest media directory
            Directory.CreateDirectory(destMediaPath);

            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath);

            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());

            // assert - src media directory is removed
            var srcDirExist = Directory.Exists(srcMediaPath);
            Assert.False(srcDirExist);

            // assert - dest media directory contains moved files and directories
            var destDirExist = Directory.Exists(destMediaPath);
            Assert.True(destDirExist);
            var destDirs = Directory.GetDirectories(destMediaPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(destDirs);
            Assert.Contains(destDirs, dir => Path.GetFileName(dir) == srcMediaPath);
            
            // assert - dest media directory contains moved files and directories in src media directory
            var destMediaPathWithSrcDir = Path.Combine(destMediaPath, srcMediaPath);
            destDirs = Directory.GetDirectories(destMediaPathWithSrcDir, "*", SearchOption.TopDirectoryOnly);
            Assert.Equal(2, destDirs.Length);
            Assert.Contains(destDirs, dir => Path.GetFileName(dir) == "dir1");
            Assert.Contains(destDirs, dir => Path.GetFileName(dir) == "dir2");
            var destFiles = Directory.GetFiles(destMediaPathWithSrcDir, "*", SearchOption.TopDirectoryOnly);
            Assert.Empty(destFiles);
            
            // assert - dest media directory contains moved files and directories in src media directory dir1
            var destMediaPathWithSrcDirAndDir1 = Path.Combine(destMediaPathWithSrcDir, "dir1");
            destDirs = Directory.GetDirectories(destMediaPathWithSrcDirAndDir1, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(destDirs);
            Assert.Contains(destDirs, dir => Path.GetFileName(dir) == "dir3");
            destFiles = Directory.GetFiles(destMediaPathWithSrcDirAndDir1, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(destFiles);
            Assert.Contains(destFiles, file => Path.GetFileName(file) == "file1.txt");
        }
        finally
        {
            DeletePaths(srcMediaPath, destMediaPath);
        }
    }
    
    [Fact]
    public async Task When_MovingADirFromAndToSameMediaWithPattern_Then_DirIsMoved()
    {
        // arrange - create paths
        var srcMediaPath = $"{Guid.NewGuid()}-local";
        var srcPath = Path.Combine(srcMediaPath, "*");
        var destMediaPath = $"{Guid.NewGuid()}-local";
        var destPath = destMediaPath;

        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create src media directory with files
            Directory.CreateDirectory(srcMediaPath);
            await LocalTestHelper.CreateDirectoriesAndFiles(srcMediaPath);

            // arrange - create dest media directory
            Directory.CreateDirectory(destMediaPath);

            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath);

            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());

            // assert - src media directory is empty
            var srcDirExist = Directory.Exists(srcMediaPath);
            Assert.True(srcDirExist);
            var srcFiles = Directory.GetFiles(srcMediaPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Empty(srcFiles);
            var srcDirs = Directory.GetDirectories(srcMediaPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Empty(srcDirs);

            // assert - dest media directory contains moved files and directories
            var destDirExist = Directory.Exists(destMediaPath);
            Assert.True(destDirExist);
            var destDirs = Directory.GetDirectories(destMediaPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Equal(2, destDirs.Length);
            Assert.Contains(destDirs, dir => Path.GetFileName(dir) == "dir1");
            Assert.Contains(destDirs, dir => Path.GetFileName(dir) == "dir2");
            var destFiles = Directory.GetFiles(destMediaPath, "*", SearchOption.TopDirectoryOnly);
            Assert.Empty(destFiles);
            
            // assert - dest media directory contains moved files and directories in src media directory dir1
            var destMediaPathWithDir1 = Path.Combine(destMediaPath, "dir1");
            destDirs = Directory.GetDirectories(destMediaPathWithDir1, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(destDirs);
            Assert.Contains(destDirs, dir => Path.GetFileName(dir) == "dir3");
            destFiles = Directory.GetFiles(destMediaPathWithDir1, "*", SearchOption.TopDirectoryOnly);
            Assert.Single(destFiles);
            Assert.Contains(destFiles, file => Path.GetFileName(file) == "file1.txt");
        }
        finally
        {
            DeletePaths(srcMediaPath, destMediaPath);
        }
    }
}