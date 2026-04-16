using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands.FsCommands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests;

public class GivenFsMkLinkCommand
{
    [Fact]
    public async Task When_CreatingLinkToFileOnPfs3Partition_Then_LinkIsCreated()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var fromPath = Path.Combine(mediaPath, "rdb", "1", "link.txt");
        const string toPath = "file.txt";
        
        // arrange - create test command helper
        using var commandHelper = new TestCommandHelper();

        // arrange - create media
        commandHelper.AddTestMedia(mediaPath, 0);
        await TestHelper.CreatePfs3FormattedDisk(commandHelper, mediaPath, 100.MB().ToSectorSize());
        
        // arrange - create file.txt file
        await RdbTestHelper.CreateFile(commandHelper, mediaPath, ["file.txt"]);
        
        // arrange - create fs mklink command
        var command = new FsMkLinkCommand(new NullLogger<FsMkLinkCommand>(), commandHelper, [], fromPath,
            toPath);

        // act - execute fs mklink command
        var result = await command.Execute(CancellationToken.None);

        // assert - fs mklink command succeeded
        Assert.True(result.IsSuccess);
        
        // assert - root directory contains 2 entries
        var entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath, 0, [])).ToList();
        Assert.Equal(2, entries.Count);
        
        // assert - file.txt entry exist and is a file
        var fileEntry = entries.FirstOrDefault(e => e.Name == "file.txt");
        Assert.NotNull(fileEntry);
        Assert.Equal(Amiga.FileSystems.EntryType.File,  fileEntry.Type);
        
        // assert - link.txt entry exist, is a file link type and has link path to file.txt
        var linkEntry = entries.FirstOrDefault(e => e.Name == "link.txt");
        Assert.NotNull(linkEntry);
        Assert.Equal(Amiga.FileSystems.EntryType.FileLink,  linkEntry.Type);
        Assert.Equal("file.txt", linkEntry.LinkPath);
    }

    [Fact]
    public async Task When_CreatingLinkToDirOnPfs3Partition_Then_LinkIsCreated()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var fromPath = Path.Combine(mediaPath, "rdb", "1", "link");
        const string toPath = "dir";
        
        // arrange - create test command helper
        using var commandHelper = new TestCommandHelper();

        // arrange - create media
        commandHelper.AddTestMedia(mediaPath, 0);
        await TestHelper.CreatePfs3FormattedDisk(commandHelper, mediaPath, 100.MB().ToSectorSize());
        
        // arrange - create file.txt file
        await RdbTestHelper.CreateFile(commandHelper, mediaPath, ["dir", "file.txt"]);
        
        // arrange - create fs mklink command
        var command = new FsMkLinkCommand(new NullLogger<FsMkLinkCommand>(), commandHelper, [], fromPath,
            toPath);

        // act - execute fs mklink command
        var result = await command.Execute(CancellationToken.None);

        // assert - fs mklink command succeeded
        Assert.True(result.IsSuccess);
        
        // assert - root directory contains 2 entries
        var entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath, 0, [])).ToList();
        Assert.Equal(2, entries.Count);
        
        // assert - dir entry exist and is a dir
        var dirEntry = entries.FirstOrDefault(e => e.Name == "dir");
        Assert.NotNull(dirEntry);
        Assert.Equal(Amiga.FileSystems.EntryType.Dir,  dirEntry.Type);
        
        // assert - link entry exist, is a dir link type and has link path to dir
        var linkEntry = entries.FirstOrDefault(e => e.Name == "link");
        Assert.NotNull(linkEntry);
        Assert.Equal(Amiga.FileSystems.EntryType.DirLink,  linkEntry.Type);
        Assert.Equal("dir", linkEntry.LinkPath);
        
        // assert - link directory contains 1 entry
        entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
            0, ["link"])).ToList();

        // assert - file.txt entry exist and is a file type
        var fileEntry = entries.FirstOrDefault(e => e.Name == "file.txt");
        Assert.NotNull(fileEntry);
        Assert.Equal(Amiga.FileSystems.EntryType.File,  fileEntry.Type);
    }
    
    [Fact]
    public async Task When_CreatingLinkToFileOnDos3Partition_Then_LinkIsCreated()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var fromPath = Path.Combine(mediaPath, "rdb", "1", "link.txt");
        const string toPath = "file.txt";
        
        // arrange - create test command helper
        using var commandHelper = new TestCommandHelper();

        // arrange - create media
        commandHelper.AddTestMedia(mediaPath, 0);
        await TestHelper.CreateDos3FormattedDisk(commandHelper, mediaPath, 100.MB().ToSectorSize());
        
        // arrange - create file.txt file
        await RdbTestHelper.CreateFile(commandHelper, mediaPath, ["file.txt"]);
        
        // arrange - create fs mklink command
        var command = new FsMkLinkCommand(new NullLogger<FsMkLinkCommand>(), commandHelper, [], fromPath,
            toPath);

        // act - execute fs mklink command
        var result = await command.Execute(CancellationToken.None);

        // assert - fs mklink command succeeded
        Assert.True(result.IsSuccess);
        
        // assert - root directory contains 2 entries
        var entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath, 0, [])).ToList();
        Assert.Equal(2, entries.Count);
        
        // assert - file.txt entry exist and is a file
        var fileEntry = entries.FirstOrDefault(e => e.Name == "file.txt");
        Assert.NotNull(fileEntry);
        Assert.Equal(Amiga.FileSystems.EntryType.File,  fileEntry.Type);
        
        // assert - link.txt entry exist, is a file link type and has link path to file.txt
        var linkEntry = entries.FirstOrDefault(e => e.Name == "link.txt");
        Assert.NotNull(linkEntry);
        Assert.Equal(Amiga.FileSystems.EntryType.FileLink,  linkEntry.Type);
        Assert.Equal("file.txt", linkEntry.LinkPath);
    }
    
    [Fact]
    public async Task When_CreatingLinkToDirOnDos3Partition_Then_LinkIsCreated()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.vhd";
        var fromPath = Path.Combine(mediaPath, "rdb", "1", "link");
        const string toPath = "dir";
        
        // arrange - create test command helper
        using var commandHelper = new TestCommandHelper();

        // arrange - create media
        commandHelper.AddTestMedia(mediaPath, 0);
        await TestHelper.CreateDos3FormattedDisk(commandHelper, mediaPath, 100.MB().ToSectorSize());
        
        // arrange - create file.txt file
        await RdbTestHelper.CreateFile(commandHelper, mediaPath, ["dir", "file.txt"]);
        
        // arrange - create fs mklink command
        var command = new FsMkLinkCommand(new NullLogger<FsMkLinkCommand>(), commandHelper, [], fromPath,
            toPath);

        // act - execute fs mklink command
        var result = await command.Execute(CancellationToken.None);

        // assert - fs mklink command succeeded
        Assert.True(result.IsSuccess);
        
        // assert - root directory contains 2 entries
        var entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath, 0, [])).ToList();
        Assert.Equal(2, entries.Count);
        
        // assert - dir entry exist and is a dir
        var dirEntry = entries.FirstOrDefault(e => e.Name == "dir");
        Assert.NotNull(dirEntry);
        Assert.Equal(Amiga.FileSystems.EntryType.Dir,  dirEntry.Type);
        
        // assert - link entry exist, is a dir link type and has link path to dir
        var linkEntry = entries.FirstOrDefault(e => e.Name == "link");
        Assert.NotNull(linkEntry);
        Assert.Equal(Amiga.FileSystems.EntryType.DirLink,  linkEntry.Type);
        Assert.Equal("dir", linkEntry.LinkPath);
        
        // assert - link directory contains 1 entry
        entries = (await RdbTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
            0, ["link"])).ToList();

        // assert - file.txt entry exist and is a file type
        var fileEntry = entries.FirstOrDefault(e => e.Name == "file.txt");
        Assert.NotNull(fileEntry);
        Assert.Equal(Amiga.FileSystems.EntryType.File,  fileEntry.Type);
    }
    
    [Fact]
    public async Task When_CreatingLinkToFileOnDos3Adf_Then_LinkIsCreated()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var fromPath = Path.Combine(mediaPath, "link.txt");
        const string toPath = "file.txt";
        
        // arrange - create test command helper
        using var commandHelper = new TestCommandHelper();

        // arrange - create media
        commandHelper.AddTestMedia(mediaPath, Amiga.FloppyDiskConstants.DoubleDensity.Size);
        await AdfTestHelper.CreateFormattedAdfDisk(commandHelper, mediaPath);
        
        // arrange - create file.txt file
        await AdfTestHelper.CreateFile(commandHelper, mediaPath, ["file.txt"]);
        
        // arrange - create fs mklink command
        var command = new FsMkLinkCommand(new NullLogger<FsMkLinkCommand>(), commandHelper, [], fromPath,
            toPath);

        // act - execute fs mklink command
        var result = await command.Execute(CancellationToken.None);

        // assert - fs mklink command succeeded
        Assert.True(result.IsSuccess);
        
        // assert - root directory contains 2 entries
        var entries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath, [])).ToList();
        Assert.Equal(2, entries.Count);
        
        // assert - file.txt entry exist and is a file
        var fileEntry = entries.FirstOrDefault(e => e.Name == "file.txt");
        Assert.NotNull(fileEntry);
        Assert.Equal(Amiga.FileSystems.EntryType.File,  fileEntry.Type);
        
        // assert - link.txt entry exist, is a file link type and has link path to file.txt
        var linkEntry = entries.FirstOrDefault(e => e.Name == "link.txt");
        Assert.NotNull(linkEntry);
        Assert.Equal(Amiga.FileSystems.EntryType.FileLink,  linkEntry.Type);
        Assert.Equal("file.txt", linkEntry.LinkPath);
    }
    
    [Fact]
    public async Task When_CreatingLinkToDirOnDos3Adf_Then_LinkIsCreated()
    {
        // arrange - paths
        var mediaPath = $"{Guid.NewGuid()}.adf";
        var fromPath = Path.Combine(mediaPath, "link");
        const string toPath = "dir";
        
        // arrange - create test command helper
        using var commandHelper = new TestCommandHelper();

        // arrange - create media
        commandHelper.AddTestMedia(mediaPath, Amiga.FloppyDiskConstants.DoubleDensity.Size);
        await AdfTestHelper.CreateFormattedAdfDisk(commandHelper, mediaPath);
        
        // arrange - create file.txt file
        await AdfTestHelper.CreateFile(commandHelper, mediaPath, ["dir", "file.txt"]);
        
        // arrange - create fs mklink command
        var command = new FsMkLinkCommand(new NullLogger<FsMkLinkCommand>(), commandHelper, [], fromPath,
            toPath);

        // act - execute fs mklink command
        var result = await command.Execute(CancellationToken.None);

        // assert - fs mklink command succeeded
        Assert.True(result.IsSuccess);
        
        // assert - root directory contains 2 entries
        var entries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath, [])).ToList();
        Assert.Equal(2, entries.Count);
        
        // assert - dir entry exist and is a dir
        var dirEntry = entries.FirstOrDefault(e => e.Name == "dir");
        Assert.NotNull(dirEntry);
        Assert.Equal(Amiga.FileSystems.EntryType.Dir,  dirEntry.Type);
        
        // assert - link entry exist, is a dir link type and has link path to dir
        var linkEntry = entries.FirstOrDefault(e => e.Name == "link");
        Assert.NotNull(linkEntry);
        Assert.Equal(Amiga.FileSystems.EntryType.DirLink,  linkEntry.Type);
        Assert.Equal("dir", linkEntry.LinkPath);
        
        // assert - link directory contains 1 entry
        entries = (await AdfTestHelper.GetEntriesFromFileSystemVolume(commandHelper, mediaPath,
            ["link"])).ToList();

        // assert - file.txt entry exist and is a file type
        var fileEntry = entries.FirstOrDefault(e => e.Name == "file.txt");
        Assert.NotNull(fileEntry);
        Assert.Equal(Amiga.FileSystems.EntryType.File,  fileEntry.Type);
    }
}