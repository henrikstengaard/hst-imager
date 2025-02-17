using System.Threading.Tasks;
using System;
using Xunit;
using Hst.Imager.Core.Commands;
using DiscUtils.Iso9660;
using System.IO;
using Hst.Amiga.Extensions;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
using System.IO.Enumeration;
using System.Threading;

namespace Hst.Imager.Core.Tests.CommandTests
{
    public class GivenAmigaFileSystemHelper
    {
        [Fact]
        public async Task When_FindFileSystemsInIsoMediaWithAdfContainingFastFileSystem_Then_FileSystemIsFound()
        {
            var isoPath = $"{Guid.NewGuid()}.iso";
            var outputPath = $"{Guid.NewGuid()}-dir";
            const string fileSystemName = "FastFileSystem";

            // arrange - create adf disk with fast file system
            byte[] adfBytes;
            using (var adfStream = new MemoryStream())
            {
                await TestHelper.CreateFormattedAdfDisk(adfStream, "Amiga");
                await TestHelper.AddFileToAdf(adfStream, "L/FastFileSystem", TestHelper.FastFileSystemDos3Bytes);
                adfBytes = adfStream.ToArray();
            }

            // arrange - create iso with adf disk
            var cdBuilder = new CDBuilder();
            cdBuilder.AddFile("Amiga.adf", adfBytes);
            using var cdStream = new MemoryStream();
            await cdBuilder.Build().CopyToAsync(cdStream);

            // arrange - add iso media
            var commandHelper = new TestCommandHelper();
            await commandHelper.AddTestMedia(isoPath, data: cdStream.ToArray());

            try
            {
                // act - find file systems in iso
                var fileSystemResult = await AmigaFileSystemHelper.FindFileSystemInMedia(commandHelper, isoPath,
                    fileSystemName, outputPath);

                // assert - find file system in media succeeded
                Assert.True(fileSystemResult.IsSuccess);

                // assert - fast file system is found in iso
                Assert.Equal("FastFileSystem", fileSystemResult.Value);
                var fileSystemBytes = await commandHelper.ReadMediaData(Path.Combine(outputPath, fileSystemResult.Value));
                Assert.Equal(TestHelper.FastFileSystemDos3Bytes, fileSystemBytes);
            }
            finally
            {
                TestHelper.DeletePaths(outputPath);
            }
        }

        [Fact]
        public async Task When_FindFileSystemsInIsoMediaWithEmptyAdf_Then_NoFileSystemIsFound()
        {
            var isoPath = $"{Guid.NewGuid()}.iso";
            var outputPath = $"{Guid.NewGuid()}-dir";
            const string fileSystemName = "FastFileSystem";

            // arrange - create empty adf disk
            byte[] adfBytes;
            using (var adfStream = new MemoryStream())
            {
                await TestHelper.CreateFormattedAdfDisk(adfStream, "Amiga");
                adfBytes = adfStream.ToArray();
            }

            // arrange - create iso with adf disk
            var cdBuilder = new CDBuilder();
            cdBuilder.AddFile("Amiga.adf", adfBytes);
            using var cdStream = new MemoryStream();
            await cdBuilder.Build().CopyToAsync(cdStream);

            // arrange - add iso media
            var commandHelper = new TestCommandHelper();
            await commandHelper.AddTestMedia(isoPath, data: cdStream.ToArray());

            try
            {
                // act - find file systems in iso
                var fileSystemResult = await AmigaFileSystemHelper.FindFileSystemInMedia(commandHelper, isoPath,
                    fileSystemName, outputPath);

                // assert - find file system in media succeeded
                Assert.True(fileSystemResult.IsSuccess);

                // assert - no fast file system is found in iso
                Assert.Equal(string.Empty, fileSystemResult.Value);
            }
            finally
            {
                TestHelper.DeletePaths(outputPath);
            }
        }

        [Fact]
        public async Task When_FindFileSystemsInLhaMediaContainingFastFileSystem_Then_FileSystemIsFound()
        {
            // arrange - paths
            var lhaPath = $"{Guid.NewGuid()}.lha";
            var outputPath = $"{Guid.NewGuid()}-dir";
            const string fileSystemName = "FastFileSystem";

            var commandHelper = new TestCommandHelper();

            // arrange - add lha with fast file system
            var lhaBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "Lha", "FastFileSystem.lha"));
            await commandHelper.AddTestMedia(lhaPath, data: lhaBytes);

            try
            {
                // act - find file systems in lha
                var fileSystemResult = await AmigaFileSystemHelper.FindFileSystemInMedia(commandHelper, lhaPath,
                    fileSystemName, outputPath);

                // assert - find file system in media succeeded
                Assert.True(fileSystemResult.IsSuccess);

                // assert - fast file system is found in lha
                Assert.Equal("FastFileSystem", fileSystemResult.Value);
                var fileSystemBytes = await commandHelper.ReadMediaData(Path.Combine(outputPath, fileSystemResult.Value));
                Assert.Equal(TestHelper.FastFileSystemDos3Bytes, fileSystemBytes);
            }
            finally
            {
                TestHelper.DeletePaths(outputPath);
            }
        }

        [Fact]
        public async Task When_FindFileSystemsInLhaMediaWithoutFastFileSystem_Then_NoFileSystemIsFound()
        {
            // arrange - paths
            var lhaPath = $"{Guid.NewGuid()}.lha";
            var outputPath = $"{Guid.NewGuid()}-dir";
            const string fileSystemName = "FastFileSystem";

            var commandHelper = new TestCommandHelper();

            // arrange - add lha without fast file system
            var lhaBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "Lha", "amiga.lha"));
            await commandHelper.AddTestMedia(lhaPath, data: lhaBytes);

            try
            {
                // act - find file systems in lha
                var fileSystemResult = await AmigaFileSystemHelper.FindFileSystemInMedia(commandHelper, lhaPath,
                    fileSystemName, outputPath);

                // assert - find file system in media succeeded
                Assert.True(fileSystemResult.IsSuccess);

                // assert - no fast file system is found in lha
                Assert.Equal(string.Empty, fileSystemResult.Value);
            }
            finally
            {
                TestHelper.DeletePaths(outputPath);
            }
        }

        [Fact]
        public async Task When_FindFileSystemsInAdfMediaContainingFastFileSystem_Then_FileSystemIsFound()
        {
            // arrange - paths
            var adfPath = $"{Guid.NewGuid()}.adf";
            var outputPath = $"{Guid.NewGuid()}-dir";
            const string fileSystemName = "FastFileSystem";

            var commandHelper = new TestCommandHelper();

            // arrange - create adf disk with fast file system
            using (var adfStream = new MemoryStream())
            {
                await TestHelper.CreateFormattedAdfDisk(adfStream, "Amiga");
                await TestHelper.AddFileToAdf(adfStream, "L/FastFileSystem", TestHelper.FastFileSystemDos3Bytes);
                await commandHelper.AddTestMedia(adfPath, data: adfStream.ToArray());
            }

            try
            {
                // act - find file systems in adf
                var fileSystemResult = await AmigaFileSystemHelper.FindFileSystemInMedia(commandHelper, adfPath,
                    fileSystemName, outputPath);

                // assert - find file system in media succeeded
                Assert.True(fileSystemResult.IsSuccess);

                // assert - fast file system is found in adf
                Assert.Equal("FastFileSystem", fileSystemResult.Value);
                var fileSystemBytes = await commandHelper.ReadMediaData(Path.Combine(outputPath, fileSystemResult.Value));
                Assert.Equal(TestHelper.FastFileSystemDos3Bytes, fileSystemBytes);
            }
            finally
            {
                TestHelper.DeletePaths(outputPath);
            }
        }

        [Fact]
        public async Task When_FindFileSystemsInEmptyAdfMedia_Then_NoFileSystemIsFound()
        {
            // arrange - paths
            var adfPath = $"{Guid.NewGuid()}.adf";
            var outputPath = $"{Guid.NewGuid()}-dir";
            const string fileSystemName = "FastFileSystem";

            var commandHelper = new TestCommandHelper();

            // arrange - create empty adf disk
            using (var adfStream = new MemoryStream())
            {
                await TestHelper.CreateFormattedAdfDisk(adfStream, "Amiga");
                await commandHelper.AddTestMedia(adfPath, data: adfStream.ToArray());
            }

            try
            {
                // act - find file systems in adf
                var fileSystemResult = await AmigaFileSystemHelper.FindFileSystemInMedia(commandHelper, adfPath,
                    fileSystemName, outputPath);

                // assert - find file system in media succeeded
                Assert.True(fileSystemResult.IsSuccess);

                // assert - no fast file system is found in adf
                Assert.Equal(string.Empty, fileSystemResult.Value);
            }
            finally
            {
                TestHelper.DeletePaths(outputPath);
            }
        }

        [Fact]
        public async Task When_FindFileSystemsInRdbMediaWithFastFileSystem_Then_FileSystemIsFound()
        {
            // arrange - paths
            var rdbPath = $"{Guid.NewGuid()}.img";
            var outputPath = $"{Guid.NewGuid()}-dir";
            const string fileSystemName = "FastFileSystem";

            var commandHelper = new TestCommandHelper();

            // arrange - create rdb disk with fast file system
            var rigidDiskBlock = RigidDiskBlock.Create(100.MB()).AddFileSystem(TestHelper.Dos3DosType,
                TestHelper.FastFileSystemDos3Bytes).AddPartition("DH0", bootable: true);
            using (var rdbStream = new MemoryStream())
            {
                await RigidDiskBlockWriter.WriteBlock(rigidDiskBlock, rdbStream);
                await commandHelper.AddTestMedia(rdbPath, data: rdbStream.ToArray());
            }

            try
            {
                // act - find file systems in rdb
                var fileSystemResult = await AmigaFileSystemHelper.FindFileSystemInMedia(commandHelper, rdbPath,
                    fileSystemName, outputPath);

                // assert - find file system in media succeeded
                Assert.True(fileSystemResult.IsSuccess);

                // assert - fast file system is found in rdb
                Assert.Equal("FastFileSystem", fileSystemResult.Value);
                var fileSystemBytes = await commandHelper.ReadMediaData(Path.Combine(outputPath, fileSystemResult.Value));
                Assert.Equal(TestHelper.FastFileSystemDos3Bytes, fileSystemBytes);
            }
            finally
            {
                TestHelper.DeletePaths(outputPath);
            }
        }
        
        [Fact]
        public async Task When_FindFileSystemsInMbrPiStormRdbMediaWithFastFileSystem_Then_FileSystemIsFound()
        {
            // arrange - paths
            var diskPath = $"{Guid.NewGuid()}.vhd";
            var diskSize = 2.GB();
            const FormatType formatType = FormatType.PiStorm;
            const string fileSystem = "dos7";
            const string assetPath = "FastFileSystem";
            const string fileSystemName = "FastFileSystem";
            var outputPath = $"{Guid.NewGuid()}-dir";

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - add fast file system file
            await testCommandHelper.AddTestMedia(assetPath, assetPath, data: TestHelper.FastFileSystemDos7Bytes);

            // arrange - add disk
            testCommandHelper.AddTestMedia(diskPath, diskSize);
            await testCommandHelper.GetWritableMedia([], diskPath, size: diskSize, create: true);

            try
            {
                // arrange - create format command
                var formatCommand = new FormatCommand(new NullLogger<FormatCommand>(), new NullLoggerFactory(),
                    testCommandHelper, new List<IPhysicalDrive>(), diskPath, formatType, fileSystem,
                    AssetAction.None, assetPath, outputPath, new Models.Size());

                // act - execute format command
                var formatResult = await formatCommand.Execute(CancellationToken.None);

                // assert - format is successful
                Assert.NotNull(formatResult);
                Assert.True(formatResult.IsSuccess);

                // act - find file systems in mbr pistorm rdb
                var fileSystemResult = await AmigaFileSystemHelper.FindFileSystemInMedia(testCommandHelper, diskPath,
                    fileSystemName, outputPath);

                // assert - find file system in media succeeded
                Assert.True(fileSystemResult.IsSuccess);

                // assert - fast file system is found in mbr pistorm rdb
                Assert.Equal("FastFileSystem", fileSystemResult.Value);
                var fileSystemBytes = await testCommandHelper.ReadMediaData(Path.Combine(outputPath, fileSystemResult.Value));
                Assert.Equal(TestHelper.FastFileSystemDos7Bytes, fileSystemBytes);
            }
            finally
            {
                TestHelper.DeletePaths(outputPath);
            }
        }
    }
}