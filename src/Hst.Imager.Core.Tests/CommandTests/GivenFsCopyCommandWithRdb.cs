using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.UaeFsDbs;
using Hst.Amiga.DataTypes.UaeMetafiles;
using Hst.Amiga.FileSystems;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.UaeMetadatas;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenFsCopyCommandWithRdb : FsCommandTestBase
{
    [Fact]
    public async Task When_CopyFilesFromRdbToLocalDirectoryWithoutUaeMetadata_Then_NoMetadataFilesAreCreated()
    {
        var srcPath = $"{Guid.NewGuid()}.vhd";
        var destPath = $"{Guid.NewGuid()}-copy";
        const UaeMetadata uaeMetadata = UaeMetadata.None;

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();
            await testCommandHelper.AddTestMedia(srcPath);
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - source disk image file with directories
            await CreatePfs3FormattedDisk(testCommandHelper, srcPath);
            await CreatePfs3DirectoriesAndFiles(testCommandHelper, srcPath);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "rdb", "dh0"), destPath, true, false, true, uaeMetadata);

            // act - copy
            var result = await fsCopyCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - directories and files copied from src matches expected paths
            var dir1Path = Path.Combine(destPath, "dir1");
            var dir2Path = Path.Combine(dir1Path, "dir2_");
            var expectedFiles = new[]
            {
                Path.Combine(dir2Path, "_AUX"),
                Path.Combine(dir2Path, "file1_"),
                Path.Combine(dir2Path, "file2_"),
                Path.Combine(dir2Path, "file3_"),
                Path.Combine(dir2Path, "file4_"),
                Path.Combine(dir2Path, "file5_"),
                Path.Combine(dir2Path, "file6_"),
                Path.Combine(dir2Path, "file7_"),
                Path.Combine(dir2Path, "file8+")
            };
            var actualFiles = Directory.GetFiles(dir1Path, "*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(destPath);
        }
    }

    [Fact]
    public async Task When_CopyFilesFromRdbToLocalDirectoryWithUaeFsDb_Then_MetadataFilesAreCreated()
    {
        var srcPath = $"{Guid.NewGuid()}.vhd";
        var destPath = $"{Guid.NewGuid()}-copy";
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();
            await testCommandHelper.AddTestMedia(srcPath);
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - source disk image file with directories
            await CreatePfs3FormattedDisk(testCommandHelper, srcPath);
            await CreatePfs3DirectoriesAndFiles(testCommandHelper, srcPath);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "rdb", "dh0"), destPath, true, false, true, uaeMetadata);

            // act - copy
            var result = await fsCopyCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);
            
            // assert - directories and files copied from src matches expected paths
            var dir1Path = Path.Combine(destPath, "dir1");
            var dir2Path = Path.Combine(dir1Path, "__uae___dir2_");
            var expectedFiles = new[]
            {
                Path.Combine(destPath, "_UAEFSDB.___"),
                Path.Combine(dir2Path, "__uae___AUX"),
                Path.Combine(dir2Path, "__uae___file1_"),
                Path.Combine(dir2Path, "__uae___file2_"),
                Path.Combine(dir2Path, "__uae___file3_"),
                Path.Combine(dir2Path, "__uae___file4_"),
                Path.Combine(dir2Path, "__uae___file5_"),
                Path.Combine(dir2Path, "__uae___file6_"),
                Path.Combine(dir2Path, "__uae___file7_"),
                Path.Combine(dir2Path, "_UAEFSDB.___"),
                Path.Combine(dir2Path, "file8+"),
                Path.Combine(dir1Path, "_UAEFSDB.___")
            };
            var actualFiles = Directory.GetFiles(destPath, "*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);

            // assert - uaefsdb in dir 1 contains dir 2
            var uaeFsDbPath = Path.Combine(destPath, "_UAEFSDB.___");
            var uaeFsDbNodes = (await ReadUaeFsDbNodes(uaeFsDbPath)).ToList();
            var amigaNames = uaeFsDbNodes.Select(x => x.AmigaName).ToArray();
            var normalNames = uaeFsDbNodes.Select(x => x.NormalName).ToArray();
            Assert.Equal(new []{"dir1"}, amigaNames);
            Assert.Equal(new []{"dir1"}, normalNames);

            // assert - dir1 uae metafile matches protection bits
            var dir1UaeFsDbNode = uaeFsDbNodes.FirstOrDefault(x => x.AmigaName == "dir1");
            Assert.NotNull(dir1UaeFsDbNode);
            Assert.Equal((uint)ProtectionBits.Write, dir1UaeFsDbNode.Mode ^ 0xf); // mask away "RWED" protection bits
            
            // assert - uaefsdb in dir 1 contains dir 2
            uaeFsDbPath = Path.Combine(dir1Path, "_UAEFSDB.___");
            uaeFsDbNodes = (await ReadUaeFsDbNodes(uaeFsDbPath)).ToList();
            amigaNames = uaeFsDbNodes.Select(x => x.AmigaName).ToArray();
            normalNames = uaeFsDbNodes.Select(x => x.NormalName).ToArray();
            Assert.Equal(new []{"dir2*"}, amigaNames);
            Assert.Equal(new []{"__uae___dir2_"}, normalNames);
            
            // assert - uaefsdb in dir 2 contains file 1-7 and aux
            uaeFsDbPath = Path.Combine(dir2Path, "_UAEFSDB.___");
            Assert.True(File.Exists(uaeFsDbPath));
            uaeFsDbNodes = (await ReadUaeFsDbNodes(uaeFsDbPath)).ToList();
            amigaNames = uaeFsDbNodes.Select(x => x.AmigaName).OrderBy(x => x).ToArray();
            normalNames = uaeFsDbNodes.Select(x => x.NormalName).OrderBy(x => x).ToArray();
            Array.Sort(amigaNames);
            Array.Sort(normalNames);
            Assert.Equal(new []
            {
                "AUX",
                "file1\\",
                "file2*",
                "file3?",
                "file4\"",
                "file5<",
                "file6>",
                "file7|",
                "file8+"
            }, amigaNames);
            Assert.Equal(new []
            {
                "__uae___AUX",
                "__uae___file1_",
                "__uae___file2_",
                "__uae___file3_",
                "__uae___file4_",
                "__uae___file5_",
                "__uae___file6_",
                "__uae___file7_",
                "file8+"
            }, normalNames);
            
            // assert - file1\ uae metafile matches protection bits and comment
            var file1UaeFsDbNode = uaeFsDbNodes.FirstOrDefault(x => x.AmigaName == "file1\\");
            Assert.NotNull(file1UaeFsDbNode);
            Assert.Equal((uint)ProtectionBits.Read, file1UaeFsDbNode.Mode ^ 0xf); // mask away "RWED" protection bits
            Assert.Equal("comment on file1", file1UaeFsDbNode.Comment);

            // assert - file8+ uae metafile matches protection bits and comment
            var file8UaeFsDbNode = uaeFsDbNodes.FirstOrDefault(x => x.AmigaName == "file8+");
            Assert.NotNull(file8UaeFsDbNode);
            Assert.Equal((uint)ProtectionBits.Script, file8UaeFsDbNode.Mode ^ 0xf); // mask away "RWED" protection bits
            Assert.Equal("another comment on file8", file8UaeFsDbNode.Comment);
        }
        finally
        {
            DeletePaths(destPath);
        }
    }

    [Fact]
    public async Task When_CopyFilesFromRdbToLocalDirectoryWithUaeMetafile_Then_MetadataFilesAreCreated()
    {
        var srcPath = $"{Guid.NewGuid()}.vhd";
        var destPath = $"{Guid.NewGuid()}-copy";
        const UaeMetadata uaeMetadata = UaeMetadata.UaeMetafile;

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();
            await testCommandHelper.AddTestMedia(srcPath);
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - source disk image file with directories
            await CreatePfs3FormattedDisk(testCommandHelper, srcPath);
            await CreatePfs3DirectoriesAndFiles(testCommandHelper, srcPath);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "rdb", "dh0"), destPath, true, false, true, uaeMetadata);

            // act - copy
            var result = await fsCopyCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);
            
            // assert - directories and files copied from src matches expected uae metafile paths
            var dir1Path = Path.Combine(destPath, "dir1");
            var dir2Path = Path.Combine(dir1Path, "dir2%2a");
            var expectedFiles = new[]
            {
                Path.Combine(dir1Path, "dir2%2a.uaem"),
                Path.Combine(dir2Path, "%41%55%58"), // AUX
                Path.Combine(dir2Path, "%41%55%58.uaem"), // AUX
                Path.Combine(dir2Path, "file1%5c"),
                Path.Combine(dir2Path, "file1%5c.uaem"),
                Path.Combine(dir2Path, "file2%2a"),
                Path.Combine(dir2Path, "file2%2a.uaem"),
                Path.Combine(dir2Path, "file3%3f"),
                Path.Combine(dir2Path, "file3%3f.uaem"),
                Path.Combine(dir2Path, "file4%22"),
                Path.Combine(dir2Path, "file4%22.uaem"),
                Path.Combine(dir2Path, "file5%3c"),
                Path.Combine(dir2Path, "file5%3c.uaem"),
                Path.Combine(dir2Path, "file6%3e"),
                Path.Combine(dir2Path, "file6%3e.uaem"),
                Path.Combine(dir2Path, "file7%7c"),
                Path.Combine(dir2Path, "file7%7c.uaem"),
                Path.Combine(dir2Path, "file8+"),
                Path.Combine(dir2Path, "file8+.uaem")
            };
            var actualFiles = Directory.GetFiles(dir1Path, "*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);

            // assert - directories and files copied from src matches expected decoded uae metafile paths
            var dir2DecodedPath = Path.Combine(dir1Path, "dir2*");
            var expectedDecodedFiles = new[]
            {
                Path.Combine(dir1Path, "dir2*.uaem"),
                Path.Combine(dir2DecodedPath, "AUX"),
                Path.Combine(dir2DecodedPath, "AUX.uaem"),
                Path.Combine(dir2DecodedPath, "file1\\"),
                Path.Combine(dir2DecodedPath, "file1\\.uaem"),
                Path.Combine(dir2DecodedPath, "file2*"),
                Path.Combine(dir2DecodedPath, "file2*.uaem"),
                Path.Combine(dir2DecodedPath, "file3?"),
                Path.Combine(dir2DecodedPath, "file3?.uaem"),
                Path.Combine(dir2DecodedPath, "file4\""),
                Path.Combine(dir2DecodedPath, "file4\".uaem"),
                Path.Combine(dir2DecodedPath, "file5<"),
                Path.Combine(dir2DecodedPath, "file5<.uaem"),
                Path.Combine(dir2DecodedPath, "file6>"),
                Path.Combine(dir2DecodedPath, "file6>.uaem"),
                Path.Combine(dir2DecodedPath, "file7|"),
                Path.Combine(dir2DecodedPath, "file7|.uaem"),
                Path.Combine(dir2DecodedPath, "file8+"),
                Path.Combine(dir2DecodedPath, "file8+.uaem")
            };
            var actualDecodedFiles = actualFiles.Select(UaeMetafileHelper.DecodeFilename).ToArray();
            Array.Sort(actualDecodedFiles);
            Assert.Equal(expectedDecodedFiles, actualDecodedFiles);

            // assert - dir1 uae metafile matches protection bits
            var dir1UaeMetafilePath = Path.Combine(destPath, "dir1.uaem");
            var dir1UaeMetafile = UaeMetafileReader.Read(await File.ReadAllBytesAsync(dir1UaeMetafilePath));
            Assert.Equal("-----w--", dir1UaeMetafile.ProtectionBits);

            // assert - file1\ uae metafile matches protection bits and comment
            var file1UaeMetafilePath = Path.Combine(dir2Path, "file1%5c.uaem");
            var file1UaeMetafile = UaeMetafileReader.Read(await File.ReadAllBytesAsync(file1UaeMetafilePath));
            Assert.Equal("----r---", file1UaeMetafile.ProtectionBits);
            Assert.Equal("comment on file1", file1UaeMetafile.Comment);
            
            // assert - file8+ uae metafile matches protection bits and comment
            var file8UaeMetafilePath = Path.Combine(dir2Path, "file8+.uaem");
            var file8UaeMetafile = UaeMetafileReader.Read(await File.ReadAllBytesAsync(file8UaeMetafilePath));
            Assert.Equal("-s------", file8UaeMetafile.ProtectionBits);
            Assert.Equal("another comment on file8", file8UaeMetafile.Comment);
        }
        finally
        {
            DeletePaths(destPath);
        }
    }

    [Fact]
    public async Task When_CopyFilesFromRdbToLocalDirectoryWithExistingDirsAndUaeFsDb_Then_MetadataFilesAreCreated()
    {
        var srcPath = $"{Guid.NewGuid()}.vhd";
        var destPath = $"{Guid.NewGuid()}-copy";
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();
            await testCommandHelper.AddTestMedia(srcPath);
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create existing dir1 directory
            var dir1Path = Path.Combine(destPath, "dir1");
            Directory.CreateDirectory(dir1Path);
            var dir2Path = Path.Combine(dir1Path, "__uae___dir2_");
            Directory.CreateDirectory(dir2Path);
            
            // arrange - create existing uaefsdb file in dir1 with dir2 node
            var uaeFsDbPath = Path.Combine(dir1Path, "_UAEFSDB.___");
            var node = new UaeFsDbNode
            {
                Version = UaeFsDbNode.NodeVersion.Version1,
                Mode = (uint)ProtectionBits.HeldResident,
                AmigaName = "dir2*",
                NormalName = "__uae___dir2_",
                Comment = string.Empty
            };
            await File.WriteAllBytesAsync(uaeFsDbPath, UaeFsDbWriter.Build(node));
            
            // arrange - source disk image file with directories
            await CreatePfs3FormattedDisk(testCommandHelper, srcPath);
            await CreatePfs3DirectoriesAndFiles(testCommandHelper, srcPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "rdb", "dh0"), destPath, true, false, true, uaeMetadata);

            // act - copy
            var result = await fsCopyCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - directories and files copied from src matches expected uae metafile paths
            var expectedDirs = new[]
            {
                dir1Path,
                dir2Path
            };
            var actualDirs = Directory.GetDirectories(destPath, "*", SearchOption.AllDirectories);
            Array.Sort(actualDirs);
            Assert.Equal(expectedDirs, actualDirs);

            var expectedFiles = new[]
            {
                Path.Combine(destPath, "_UAEFSDB.___"),
                Path.Combine(dir2Path, "__uae___AUX"),
                Path.Combine(dir2Path, "__uae___file1_"),
                Path.Combine(dir2Path, "__uae___file2_"),
                Path.Combine(dir2Path, "__uae___file3_"),
                Path.Combine(dir2Path, "__uae___file4_"),
                Path.Combine(dir2Path, "__uae___file5_"),
                Path.Combine(dir2Path, "__uae___file6_"),
                Path.Combine(dir2Path, "__uae___file7_"),
                Path.Combine(dir2Path, "_UAEFSDB.___"),
                Path.Combine(dir2Path, "file8+"),
                Path.Combine(dir1Path, "_UAEFSDB.___")
            };
            var actualFiles = Directory.GetFiles(destPath, "*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);
            
            // assert - uaefsdb in dir 1 contains dir 2
            var uaeFsDbNodes = (await ReadUaeFsDbNodes(uaeFsDbPath)).ToList();
            var amigaNames = uaeFsDbNodes.Select(x => x.AmigaName).OrderBy(x => x).ToArray();
            var normalNames = uaeFsDbNodes.Select(x => x.NormalName).OrderBy(x => x).ToArray();
            Array.Sort(amigaNames);
            Array.Sort(normalNames);
            Assert.Equal(new [] { "dir2*" }, amigaNames);
            Assert.Equal(new [] { "__uae___dir2_" }, normalNames);
            
            // assert - dir2* uae metafile matches protection bits and comment
            var dir2UaeFsDbNode = uaeFsDbNodes.FirstOrDefault(x => x.AmigaName == "dir2*");
            Assert.NotNull(dir2UaeFsDbNode);
            Assert.Equal((uint)ProtectionBits.HeldResident, dir2UaeFsDbNode.Mode);
            Assert.Equal(string.Empty, dir2UaeFsDbNode.Comment);
        }
        finally
        {
            DeletePaths(destPath);
        }
    }

    [Fact]
    public async Task When_CopySingleFileInRootFromRdbToRdbNotRecursive_Then_OnlyFileIsCopied()
    {
        var srcPath = $"{Guid.NewGuid()}.vhd";
        var destPath = $"{Guid.NewGuid()}.vhd";
        const UaeMetadata uaeMetadata = UaeMetadata.None;
        const bool recursive = false;

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();
        await testCommandHelper.AddTestMedia(srcPath);
        await testCommandHelper.AddTestMedia(destPath);
        var cancellationTokenSource = new CancellationTokenSource();

        // arrange - source disk image file with directories
        await CreatePfs3FormattedDisk(testCommandHelper, srcPath);
        await CreatePfs3DirectoriesAndFilesWithoutUaeMetadata(testCommandHelper, srcPath);
        await CreatePfs3FormattedDisk(testCommandHelper, destPath);

        // arrange - create fs copy command
        var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(),
            Path.Combine(srcPath, "rdb", "dh0", "file1"), 
            Path.Combine(destPath, "rdb", "dh0"), recursive, false, true, uaeMetadata);

        // act - copy
        var result = await fsCopyCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);

        // assert - files
        var entries = (await ListPfs3Entries(testCommandHelper, destPath, Array.Empty<string>())).ToList();
        Assert.Single(entries);
        Assert.Equal(1, entries.Count(x => x.Type == EntryType.File && x.Name == "file1"));
    }

    [Fact]
    public async Task When_CopySingleFileInSubdirectoryFromRdbToRdbRecursive_Then_OnlyFileIsCopied()
    {
        var srcPath = $"{Guid.NewGuid()}.vhd";
        var destPath = $"{Guid.NewGuid()}.vhd";
        const UaeMetadata uaeMetadata = UaeMetadata.None;
        const bool recursive = true;

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();
        await testCommandHelper.AddTestMedia(srcPath);
        await testCommandHelper.AddTestMedia(destPath);
        var cancellationTokenSource = new CancellationTokenSource();

        // arrange - source disk image file with directories
        await CreatePfs3FormattedDisk(testCommandHelper, srcPath);
        await CreatePfs3DirectoriesAndFilesWithoutUaeMetadata(testCommandHelper, srcPath);
        await CreatePfs3FormattedDisk(testCommandHelper, destPath);

        // arrange - create fs copy command
        var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
            new List<IPhysicalDrive>(),
            Path.Combine(srcPath, "rdb", "dh0", "file2*"),
            Path.Combine(destPath, "rdb", "dh0"), recursive, false, true, uaeMetadata);

        // act - copy
        var result = await fsCopyCommand.Execute(cancellationTokenSource.Token);
        Assert.True(result.IsSuccess);

        // assert - root directory only contains dir2
        var rootEntries = (await ListPfs3Entries(testCommandHelper, destPath, Array.Empty<string>())).ToList();
        Assert.Single(rootEntries);
        Assert.Equal(1, rootEntries.Count(x => x.Type == EntryType.Dir && x.Name == "dir2"));

        // assert - files
        var subDirectoryEntries = (await ListPfs3Entries(testCommandHelper, destPath, new[] { "dir2" })).ToList();
        Assert.Single(subDirectoryEntries);
        Assert.Equal(1, subDirectoryEntries.Count(x => x.Type == EntryType.File && x.Name == "file2"));
    }

    [Fact]
    public async Task When_CopySingleFileInRootFromLocalDirectoryToRdbNotRecursive_Then_OnlyFileIsCopied()
    {
        var srcPath = $"{Guid.NewGuid()}-local";
        var destPath = $"{Guid.NewGuid()}.vhd";
        const UaeMetadata uaeMetadata = UaeMetadata.None;
        const bool recursive = false;

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();
            await testCommandHelper.AddTestMedia(destPath);
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - source local file with directories
            var dir1Path = Path.Combine(srcPath, "dir1");
            var dir2Path = Path.Combine(srcPath, "dir2");
            Directory.CreateDirectory(dir1Path);
            Directory.CreateDirectory(dir2Path);
            var file1Path = Path.Combine(srcPath, "file1");
            var file2Path = Path.Combine(dir2Path, "file2");
            await File.WriteAllTextAsync(file1Path, string.Empty);
            await File.WriteAllTextAsync(file2Path, string.Empty);

            // arrange - dest formatted disk
            await CreatePfs3FormattedDisk(testCommandHelper, destPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "file1"),
                Path.Combine(destPath, "rdb", "dh0"), recursive, false, true, uaeMetadata);

            // act - copy
            var result = await fsCopyCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - files
            var entries = (await ListPfs3Entries(testCommandHelper, destPath, Array.Empty<string>())).ToList();
            Assert.Single(entries);
            Assert.Equal(1, entries.Count(x => x.Type == EntryType.File && x.Name == "file1"));
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    [Fact]
    public async Task When_CopySingleFileInSubdirectoryFromLocalDirectoryToRdbRecursive_Then_OnlyFileIsCopied()
    {
        var srcPath = $"{Guid.NewGuid()}-local";
        var destPath = $"{Guid.NewGuid()}.vhd";
        const UaeMetadata uaeMetadata = UaeMetadata.None;
        const bool recursive = true;

        try
        {
            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();
            await testCommandHelper.AddTestMedia(destPath);
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - source local file with directories
            var dir1Path = Path.Combine(srcPath, "dir1");
            var dir2Path = Path.Combine(srcPath, "dir2");
            Directory.CreateDirectory(dir1Path);
            Directory.CreateDirectory(dir2Path);
            var file1Path = Path.Combine(srcPath, "file1");
            var file2Path = Path.Combine(dir2Path, "file2");
            await File.WriteAllTextAsync(file1Path, string.Empty);
            await File.WriteAllTextAsync(file2Path, string.Empty);

            // arrange - dest formatted disk
            await CreatePfs3FormattedDisk(testCommandHelper, destPath);

            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "file2*"),
                Path.Combine(destPath, "rdb", "dh0"), recursive, false, true, uaeMetadata);

            // act - copy
            var result = await fsCopyCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - root directory only contains dir2
            var rootEntries = (await ListPfs3Entries(testCommandHelper, destPath, Array.Empty<string>())).ToList();
            Assert.Single(rootEntries);
            Assert.Equal(1, rootEntries.Count(x => x.Type == EntryType.Dir && x.Name == "dir2"));

            // assert - files
            var subDirectoryEntries = (await ListPfs3Entries(testCommandHelper, destPath, new[] { "dir2" })).ToList();
            Assert.Single(subDirectoryEntries);
            Assert.Equal(1, subDirectoryEntries.Count(x => x.Type == EntryType.File && x.Name == "file2"));
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    [Theory]
    [InlineData("rdb\\dh0", ModifierEnum.None, "rdb\\dh0", false)]
    [InlineData("rdb\\dh0\\+ file.txt", ModifierEnum.None, "rdb\\dh0\\+ file.txt", false)]
    [InlineData("+bs\\rdb\\dh0", ModifierEnum.ByteSwap, "rdb\\dh0", true)]
    [InlineData("+bs\\rdb\\dh0\\+ file.txt", ModifierEnum.ByteSwap, "rdb\\dh0\\+ file.txt", true)]
    public async Task When_ResolveMediaWithImg_Then_PathsAndModifersMatch(string path, ModifierEnum expectedModifiers,
        string expectedFileSystemPath, bool expectedByteSwap)
    {
        var imgPath = Path.GetFullPath($"{Guid.NewGuid()}.img");

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        try
        {
            // arrange - empty image
            await File.WriteAllTextAsync(imgPath, string.Empty);

            // act - resolve media
            var result = testCommandHelper.ResolveMedia(Path.Combine(imgPath, path));

            // assert
            Assert.True(result.IsSuccess);
            Assert.Equal(imgPath, result.Value.MediaPath);
            Assert.Equal(expectedFileSystemPath, result.Value.FileSystemPath);
            Assert.Equal(expectedModifiers, result.Value.Modifiers);
            Assert.Equal(expectedByteSwap, result.Value.ByteSwap);
        }
        finally
        {
            DeletePaths(imgPath);
        }
    }

    [Theory]
    [InlineData("rdb\\dh0", "rdb\\dh0", false)]
    [InlineData("rdb\\dh0\\+ file.txt", "rdb\\dh0\\+ file.txt", false)]
    [InlineData("+bs\\rdb\\dh0", "rdb\\dh0", true)]
    [InlineData("+bs\\rdb\\dh0\\+ file.txt", "rdb\\dh0\\+ file.txt", true)]
    public void When_ResolveMediaWithPhysicalDrive_Then_PathsAndModifersMatch(string path, string expectedFileSystemPath, bool expectedByteSwap)
    {
        var physicalDrivePath = "\\\\.\\PhysicalDrive4";

        // arrange - test command helper
        var testCommandHelper = new TestCommandHelper();

        // act - resolve media
        var result = testCommandHelper.ResolveMedia(Path.Combine(physicalDrivePath, path));

        // assert
        Assert.True(result.IsSuccess);
        Assert.Equal(physicalDrivePath, result.Value.MediaPath);
        Assert.Equal(expectedFileSystemPath, result.Value.FileSystemPath);
        Assert.Equal(expectedByteSwap, result.Value.ByteSwap);
    }

    private static async Task<IEnumerable<UaeFsDbNode>> ReadUaeFsDbNodes(string uaeFsDbPath)
    {
        var uaeFsDbBytes = await File.ReadAllBytesAsync(uaeFsDbPath);
        var uaeFsDbNodes = new List<UaeFsDbNode>();
        var offset = 0;
        while (offset + Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbNodeVersion1Size <= uaeFsDbBytes.Length)
        {
            uaeFsDbNodes.Add(UaeFsDbReader.Read(uaeFsDbBytes, offset));
            offset += Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbNodeVersion1Size;
        }

        return uaeFsDbNodes;
    }
    
    private async Task CreatePfs3DirectoriesAndFiles(TestCommandHelper testCommandHelper, string path)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
        if (mediaResult.IsFaulted)
        {
            throw new IOException(mediaResult.Error.ToString());
        }

        using var media = mediaResult.Value;
        var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;

        await using var pfs3Volume = await MountPfs3Volume(stream);
        await pfs3Volume.CreateDirectory("dir1");
        await pfs3Volume.SetProtectionBits("dir1", ProtectionBits.Write);
        await pfs3Volume.ChangeDirectory("dir1");
        await pfs3Volume.CreateDirectory("dir2*");
        await pfs3Volume.ChangeDirectory("dir2*");
        await pfs3Volume.CreateFile("file1\\");
        await pfs3Volume.SetProtectionBits("file1\\", ProtectionBits.Read);
        await pfs3Volume.SetComment("file1\\", "comment on file1");
        await pfs3Volume.CreateFile("file2*");
        await pfs3Volume.CreateFile("file3?");
        await pfs3Volume.CreateFile("file4\"");
        await pfs3Volume.CreateFile("file5<");
        await pfs3Volume.CreateFile("file6>");
        await pfs3Volume.CreateFile("file7|");
        await pfs3Volume.CreateFile("file8+");
        await pfs3Volume.SetProtectionBits("file8+", ProtectionBits.Script);
        await pfs3Volume.SetComment("file8+", "another comment on file8");
        await pfs3Volume.CreateFile("AUX");
    }

    private async Task CreatePfs3DirectoriesAndFilesWithoutUaeMetadata(TestCommandHelper testCommandHelper, string path)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
        if (mediaResult.IsFaulted)
        {
            throw new IOException(mediaResult.Error.ToString());
        }

        using var media = mediaResult.Value;
        var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;

        await using var pfs3Volume = await MountPfs3Volume(stream);
        await pfs3Volume.CreateDirectory("dir1");
        await pfs3Volume.CreateFile("file1");
        await pfs3Volume.CreateDirectory("dir2");
        await pfs3Volume.ChangeDirectory("dir2");
        await pfs3Volume.CreateFile("file2");
    }


    private async Task<IEnumerable<Entry>> ListPfs3Entries(TestCommandHelper testCommandHelper, string path, string[] subDirectories)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
        if (mediaResult.IsFaulted)
        {
            throw new IOException(mediaResult.Error.ToString());
        }

        using var media = mediaResult.Value;
        var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;

        await using var pfs3Volume = await MountPfs3Volume(stream);

        foreach (var subDirectory in subDirectories)
        {
            await pfs3Volume.ChangeDirectory(subDirectory);
        }

        return await pfs3Volume.ListEntries();
    }
}