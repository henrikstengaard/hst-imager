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

public class GivenFsMoveCommandWithAdf : FsCommandTestBase
{
    [Fact]
    public async Task When_MovingAFileOnSameMedia_Then_FileIsMoved()
    {
        // arrange - create paths
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(mediaPath, "file1.txt");
        var destPath = Path.Combine(mediaPath, "file2.txt");
        const bool forceOverwrite = false;

        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create formatted adf disk and src file
            await AdfTestHelper.CreateFormattedAdfDisk(commandHelper, mediaPath);
            await AdfTestHelper.CreateFile(commandHelper, mediaPath, ["file1.txt"]);

            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite);

            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());

            // assert - source file is deleted and destination file is created
            commandHelper.ClearActiveMedias();
            var entries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
                [])).ToList();
            Assert.DoesNotContain(entries, entry => entry.Name == "file1.txt");
            Assert.Contains(entries, entry => entry.Name == "file2.txt");
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
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(mediaPath, "file1.txt");
        var destPath = Path.Combine(mediaPath, "file2.txt");
        const bool forceOverwrite = false;

        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create formatted adf disk
            await AdfTestHelper.CreateFormattedAdfDisk(commandHelper, mediaPath);

            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite);

            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);

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
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(mediaPath, "file1.txt");
        var destPath = Path.Combine(mediaPath, "file2.txt");
        const bool forceOverwrite = false;

        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create formatted adf disk and src file
            await AdfTestHelper.CreateFormattedAdfDisk(commandHelper, mediaPath);
            await AdfTestHelper.CreateFile(commandHelper, mediaPath, ["file1.txt"]);
            
            // arrange - create dest file
            await AdfTestHelper.CreateFile(commandHelper, mediaPath, ["file2.txt"]);

            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite);

            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);

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
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(mediaPath, "file1.txt");
        var destPath = Path.Combine(mediaPath, "file2.txt");
        const bool forceOverwrite = true;

        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create formatted adf disk and src file
            await AdfTestHelper.CreateFormattedAdfDisk(commandHelper, mediaPath);
            await AdfTestHelper.CreateFile(commandHelper, mediaPath, ["file1.txt"]);

            // arrange - create dest file
            await AdfTestHelper.CreateFile(commandHelper, mediaPath, ["file2.txt"]);

            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite);

            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());

            // assert - source file is deleted and destination file is created
            commandHelper.ClearActiveMedias();
            var entries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
                [])).ToList();
            Assert.DoesNotContain(entries, entry => entry.Name == "file1.txt");
            Assert.Contains(entries, entry => entry.Name == "file2.txt");
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
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(mediaPath, "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir1");
        const bool forceOverwrite = true;

        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create formatted adf disk and src file
            await AdfTestHelper.CreateFormattedAdfDisk(commandHelper, mediaPath);
            await AdfTestHelper.CreateFile(commandHelper, mediaPath, ["file1.txt"]);

            // arrange - create dest dir
            await AdfTestHelper.CreateDirectory(commandHelper, mediaPath, ["dir1"]);

            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite);

            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);
            commandHelper.ClearActiveMedias();

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());

            // assert - source file is deleted from root directory
            var entries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
                [])).ToList();
            Assert.Single(entries);
            Assert.DoesNotContain(entries, entry => entry.Name == "file1.txt");
            Assert.Contains(entries, entry => entry.Name == "dir1");
            
            // assert - destination file is created in dest dir
            entries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
                ["dir1"])).ToList();
            Assert.Contains(entries, entry => entry.Name == "file1.txt");
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_MovingFileFromSrcAdfToDestAdf_Then_FileIsMoved()
    {
        // arrange - create paths
        var srcMediaPath = $"{Guid.NewGuid()}.adf";
        var destMediaPath = $"{Guid.NewGuid()}.adf";
        var srcPath = Path.Combine(srcMediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(destMediaPath, "dir2");

        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create source adf disk
            await AdfTestHelper.CreateFormattedAdfDisk(commandHelper, srcMediaPath);
            await AdfTestHelper.CreateDirectoriesAndFiles(commandHelper, srcMediaPath);
            
            // arrange - create destination adf disk
            await AdfTestHelper.CreateFormattedAdfDisk(commandHelper, destMediaPath);
            await AdfTestHelper.CreateDirectoriesAndFiles(commandHelper, destMediaPath);

            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath);

            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());

            // assert - source file is deleted
            commandHelper.ClearActiveMedias();
            var srcEntries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(commandHelper, srcMediaPath,
                ["dir1"])).ToList();
            Assert.Single(srcEntries);
            Assert.Contains(srcEntries, entry => entry.Name == "dir3");
            
            // assert - destination file is created
            var destEntries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(commandHelper,
                destMediaPath, ["dir2"])).ToList();
            Assert.DoesNotContain(srcEntries, entry => entry.Name == "file1.txt");
            Assert.Contains(destEntries, entry => entry.Name == "file1.txt");
        }
        finally
        {
            DeletePaths(srcMediaPath, destMediaPath);
        }
    }
    
    [Fact]
    public async Task When_MovingAUaeFsDbAmigaNameFromLocalDirectoryToAdf_Then_FileIsMoved()
    {
        // arrange - create paths
        var srcMediaPath = $"{Guid.NewGuid()}-local";
        const string amigaName = "file?";
        var srcPath = Path.Combine(srcMediaPath, amigaName);
        const string comment = "file comment";
        const string destFilename = "file.txt";
        var destMediaPath = $"{Guid.NewGuid()}.adf";
        var destPath = Path.Combine(destMediaPath, destFilename);
        const bool forceOverwrite = false;
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create src media directory
            Directory.CreateDirectory(srcMediaPath);

            // arrange - create src file and uaefsdb node
            var safeName = UaeFsDbNodeHelper.MakeSafeFilename(amigaName);
            var normalName = UaeFsDbNodeHelper.CreateUniqueFileNormalName(srcMediaPath, safeName);
            var srcNormalNamePath = Path.Combine(srcMediaPath, normalName);
            var uaeFsDbPath = Path.Combine(srcMediaPath, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);
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
            
            // arrange - create dest media adf disk
            await commandHelper.AddTestMedia(destMediaPath);
            await TestHelper.CreateFormattedAdfDisk(commandHelper, destMediaPath, "Amiga");

            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite, uaeMetadata);
            
            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());

            // assert - src file is deleted
            commandHelper.ClearActiveMedias();
            var srcEntries = Directory.GetFiles(srcMediaPath, "*", SearchOption.AllDirectories).ToList();
            Assert.Empty(srcEntries);
            
            // assert - dest file is created with correct amiga name and comment
            var destEntries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(commandHelper, destMediaPath,
                [])).ToList();
            Assert.Single(destEntries);
            var destEntry = destEntries[0];
            Assert.Equal(destFilename, destEntry.Name);
            Assert.Equal(comment, destEntry.Comment);
        }
        finally
        {
            DeletePaths(srcMediaPath, destMediaPath);
        }
    }
    
    [Fact]
    public async Task When_MovingAUaeMetafileAmigaNameFromLocalDirectoryToAdf_Then_FileIsMoved()
    {
        // arrange - create paths
        var srcMediaPath = $"{Guid.NewGuid()}-local";
        const string amigaName = "file?";
        var srcEncodedName = UaeMetafileHelper.EncodeFilenameSpecialChars(amigaName);
        var srcPath = Path.Combine(srcMediaPath, amigaName);
        var srcEncodedPath = Path.Combine(srcMediaPath, srcEncodedName);
        const string comment = "file comment";
        const string destFilename = "file.txt";
        var destMediaPath = $"{Guid.NewGuid()}.adf";
        var destPath = Path.Combine(destMediaPath, destFilename);
        const bool forceOverwrite = false;
        const UaeMetadata uaeMetadata = UaeMetadata.UaeMetafile;

        try
        {
            // arrange - create test command helper
            using var commandHelper = new TestCommandHelper();
            
            // arrange - create src media directory
            Directory.CreateDirectory(srcMediaPath);

            // arrange - create src file and uae metafile
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
            
            // arrange - create dest media adf disk
            await commandHelper.AddTestMedia(destMediaPath);
            await TestHelper.CreateFormattedAdfDisk(commandHelper, destMediaPath, "Amiga");

            // arrange - create fs move command
            var command = new FsMoveCommand(new NullLogger<FsMoveCommand>(), commandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, forceOverwrite, uaeMetadata);
            
            // act - execute fs move command
            var result = await command.Execute(CancellationToken.None);

            // assert - result is success
            Assert.True(result.IsSuccess, result.Error?.ToString());

            // assert - src file is deleted
            commandHelper.ClearActiveMedias();
            var srcEntries = Directory.GetFiles(srcMediaPath, "*", SearchOption.AllDirectories).ToList();
            Assert.Empty(srcEntries);
            
            // assert - dest file is created with correct amiga name and comment
            var destEntries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(commandHelper, destMediaPath,
                [])).ToList();
            Assert.Single(destEntries);
            var destEntry = destEntries[0];
            Assert.Equal(destFilename, destEntry.Name);
            Assert.Equal(comment, destEntry.Comment);
        }
        finally
        {
            DeletePaths(srcMediaPath, destMediaPath);
        }
    }
}