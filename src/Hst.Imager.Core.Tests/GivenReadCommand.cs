using System.IO.Compression;
using Hst.Core.Extensions;

namespace Hst.Imager.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Core;
    using Commands;
    using Models;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class GivenReadCommand : CommandTestBase
    {
        [Fact]
        public async Task WhenReadPhysicalDriveToImgThenReadDataIsIdentical()
        {
            // arrange - paths and test command helper
            var sourcePath = TestCommandHelper.PhysicalDrivePath;
            var destinationPath = $"{Guid.NewGuid()}.img";
            var testCommandHelper = new TestCommandHelper();

            // arrange - create source physical drive
            testCommandHelper.AddTestMediaWithData(sourcePath, ImageSize);

            // arrange - create destination img
            await testCommandHelper.AddTestMedia(destinationPath);

            // act - read source physical drive to destination img
            var cancellationTokenSource = new CancellationTokenSource();
            var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(), 0, false, false, 0);
            DataProcessedEventArgs dataProcessedEventArgs = null;
            readCommand.DataProcessed += (_, args) => { dataProcessedEventArgs = args; };
            var result = await readCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            Assert.NotNull(dataProcessedEventArgs);
            Assert.NotEqual(0, dataProcessedEventArgs.PercentComplete);
            Assert.NotEqual(0, dataProcessedEventArgs.BytesProcessed);
            Assert.Equal(0, dataProcessedEventArgs.BytesRemaining);
            Assert.NotEqual(0, dataProcessedEventArgs.BytesTotal);

            // assert data is identical
            var sourceBytes = await testCommandHelper.ReadMediaData(sourcePath);
            var destinationBytes = await testCommandHelper.ReadMediaData(destinationPath);
            Assert.Equal(sourceBytes, destinationBytes);
        }

        [Fact]
        public async Task WhenReadPhysicalDriveToImgWithSizeThenReadDataIsIdentical()
        {
            // arrange - paths, size to read and test command helper
            var sourcePath = TestCommandHelper.PhysicalDrivePath;
            var destinationPath = $"{Guid.NewGuid()}.img";
            var size = 16 * 512;
            var testCommandHelper = new TestCommandHelper();

            // arrange - create source physical drive
            testCommandHelper.AddTestMediaWithData(sourcePath, ImageSize);

            // arrange - create destination img
            await testCommandHelper.AddTestMedia(destinationPath);

            // act - read source physical drive to destination img
            var cancellationTokenSource = new CancellationTokenSource();
            var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(size, Unit.Bytes), 0, false, false,
                0);
            var result = await readCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert data is identical
            var sourceBytes = (await testCommandHelper.ReadMediaData(sourcePath)).Take(size).ToArray();
            Assert.Equal(size, sourceBytes.Length);
            var destinationBytes = await testCommandHelper.ReadMediaData(destinationPath);
            Assert.Equal(sourceBytes, destinationBytes);
        }

        [Fact]
        public async Task WhenReadPhysicalDriveToImg2TimesAndIncreasingSizeThenReadDataIsIdentical2()
        {
            // arrange - paths, sizes to read and test command helper
            var sourcePath = TestCommandHelper.PhysicalDrivePath;
            var destinationPath = $"{Guid.NewGuid()}.img";
            var firstReadSize = 16 * 512;
            var secondReadSize = firstReadSize * 2;
            var testCommandHelper = new TestCommandHelper();

            // arrange - create source physical drive
            testCommandHelper.AddTestMediaWithData(sourcePath, ImageSize);

            // arrange - create destination img
            await testCommandHelper.AddTestMedia(destinationPath);

            // arrange - read command
            var cancellationTokenSource = new CancellationTokenSource();
            var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(firstReadSize, Unit.Bytes), 0, false,
                false, 0);

            // act - read source physical drive to destination img 1st time
            var result = await readCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - data is identical
            var sourceBytes = (await testCommandHelper.ReadMediaData(sourcePath)).Take(firstReadSize).ToArray();
            Assert.Equal(firstReadSize, sourceBytes.Length);
            var destinationBytes = await testCommandHelper.ReadMediaData(destinationPath);
            Assert.Equal(sourceBytes, destinationBytes);

            readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(secondReadSize, Unit.Bytes), 0, false,
                false, 0);

            // act - read source physical drive to destination img 2nd time
            result = await readCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - data is identical
            sourceBytes = (await testCommandHelper.ReadMediaData(sourcePath)).Take(secondReadSize).ToArray();
            Assert.Equal(secondReadSize, sourceBytes.Length);
            destinationBytes = await testCommandHelper.ReadMediaData(destinationPath);
            Assert.Equal(sourceBytes, destinationBytes);
        }

        [Fact]
        public async Task WhenReadPhysicalDriveToVhd2TimesAndIncreasingSizeThenReadDataIsIdentical()
        {
            // arrange - paths, sizes to read and test command helper
            var sourcePath = TestCommandHelper.PhysicalDrivePath;
            var destinationPath = $"{Guid.NewGuid()}.vhd";
            var firstReadSize = 16 * 512;
            var secondReadSize = firstReadSize * 2;
            var testCommandHelper = new TestCommandHelper();

            try
            {
                // arrange - create source physical drive
                testCommandHelper.AddTestMediaWithData(sourcePath, ImageSize);

                // arrange - read command read 8192 bytes
                var cancellationTokenSource = new CancellationTokenSource();
                var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(firstReadSize, Unit.Bytes), 0,
                    false, false, 0);

                // act - read source physical drive to destination vhd 1st time
                var result = await readCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - data is identical
                var sourceBytes = (await testCommandHelper.ReadMediaData(sourcePath)).Take(firstReadSize).ToArray();
                Assert.Equal(firstReadSize, sourceBytes.Length);
                var destinationBytes = await testCommandHelper.ReadMediaData(destinationPath);
                Assert.Equal(sourceBytes, destinationBytes);

                // arrange - clear active medias to avoid source and destination being reused between commands
                testCommandHelper.ClearActiveMedias();

                // arrange - read command read 16384 bytes
                readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(secondReadSize, Unit.Bytes), 0,
                    false, false, 0);

                // act - read source physical drive to destination vhd 2nd time
                result = await readCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - data is identical
                sourceBytes = (await testCommandHelper.ReadMediaData(sourcePath)).Take(secondReadSize).ToArray();
                Assert.Equal(secondReadSize, sourceBytes.Length);
                destinationBytes = await testCommandHelper.ReadMediaData(destinationPath);
                Assert.Equal(sourceBytes, destinationBytes);
            }
            finally
            {
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
            }
        }

        [Fact]
        public async Task WhenReadPhysicalDriveToVhdThenReadDataIsIdentical()
        {
            // arrange - paths and test command helper
            var sourcePath = TestCommandHelper.PhysicalDrivePath;
            var destinationPath = $"{Guid.NewGuid()}.vhd";
            var testCommandHelper = new TestCommandHelper();

            try
            {
                // arrange - create source physical drive
                testCommandHelper.AddTestMediaWithData(sourcePath, ImageSize);

                // arrange - read command
                var cancellationTokenSource = new CancellationTokenSource();
                var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
                    Enumerable.Empty<IPhysicalDrive>(), sourcePath, destinationPath, new Size(), 0, false, false, 0);

                // act - read source physical drive to destination vhd
                var result = await readCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // get source bytes
                var sourceBytes = await testCommandHelper.ReadMediaData(sourcePath);

                // get destination bytes
                var destinationBytes =
                    await ReadMediaBytes(testCommandHelper, destinationPath, ImageSize);
                var destinationPathSize = new FileInfo(destinationPath).Length;

                // assert length is not the same (vhd file format different than img) and bytes are the same
                Assert.NotEqual(sourceBytes.Length, destinationPathSize);
                Assert.Equal(sourceBytes, destinationBytes);
            }
            finally
            {
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
            }
        }

        [Fact]
        public async Task WhenReadPhysicalDriveToVhdWithSizeThenReadDataIsIdentical()
        {
            // arrange - paths, size to read and test command helper
            var sourcePath = TestCommandHelper.PhysicalDrivePath;
            var destinationPath = $"{Guid.NewGuid()}.vhd";
            var size = 16 * 512;
            var testCommandHelper = new TestCommandHelper();

            try
            {
                // arrange - create source physical drive
                testCommandHelper.AddTestMediaWithData(sourcePath, ImageSize);

                // arrange - read command
                var cancellationTokenSource = new CancellationTokenSource();
                var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
                    Enumerable.Empty<IPhysicalDrive>(), sourcePath, destinationPath, new Size(size, Unit.Bytes), 0,
                    false, false, 0);

                // act - read source physical drive to destination vhd
                var result = await readCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // get source bytes
                var sourceBytes = (await testCommandHelper.ReadMediaData(sourcePath)).Take(size).ToArray();
                Assert.Equal(size, sourceBytes.Length);

                // get destination bytes
                var destinationBytes = await testCommandHelper.ReadMediaData(destinationPath);

                // assert - source bytes are equal to destination bytes
                destinationBytes = destinationBytes.Take(size).ToArray();
                Assert.Equal(sourceBytes.Length, destinationBytes.Length);
                Assert.Equal(sourceBytes, destinationBytes);
            }
            finally
            {
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
            }
        }

        [Fact]
        public async Task WhenReadPhysicalDriveToVhdWithSizeNotDividableBy512ThenReadDataIsIdentical()
        {
            // arrange - paths, size to read and test command helper
            var srcPath = TestCommandHelper.PhysicalDrivePath;
            var destPath = $"{Guid.NewGuid()}.vhd";
            var size = 1000000;
            var testCommandHelper = new TestCommandHelper();

            try
            {
                // arrange - create source physical drive
                testCommandHelper.AddTestMediaWithData(srcPath, 10.MB());

                // arrange - read command
                var cancellationTokenSource = new CancellationTokenSource();
                var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
                    Enumerable.Empty<IPhysicalDrive>(), srcPath, destPath, new Size(size, Unit.Bytes), 0, false, false,
                    0);

                // act - read source physical drive to destination vhd
                var result = await readCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // get source bytes
                var sourceBytes = (await testCommandHelper.ReadMediaData(srcPath)).Take(size).ToArray();
                Assert.Equal(size, sourceBytes.Length);

                // get destination bytes
                var destinationBytes = await testCommandHelper.ReadMediaData(destPath);

                // assert - destination bytes are larger than size and dividable by 512
                Assert.True(destinationBytes.Length > size);
                Assert.True(destinationBytes.Length % 512 == 0);

                // assert - destination bytes are equal to size rounded to next sector
                Assert.Equal(size + (512 - size % 512), destinationBytes.Length);

                // assert - source bytes are equal to destination bytes
                destinationBytes = destinationBytes.Take(size).ToArray();
                Assert.Equal(sourceBytes.Length, destinationBytes.Length);
                Assert.True(sourceBytes.SequenceEqual(destinationBytes));
            }
            finally
            {
                if (File.Exists(destPath))
                {
                    File.Delete(destPath);
                }
            }
        }

        [Fact]
        public async Task WhenReadPhysicalDriveToZipCompressedImgThenDataIsIdentical()
        {
            var data = TestDataHelper.CreateTestData(10.MB());
            var sourcePath = TestCommandHelper.PhysicalDrivePath;
            var destinationPath = $"{Guid.NewGuid()}.img.zip";

            try
            {
                var testCommandHelper = new TestCommandHelper();

                // arrange - create source physical drive
                await testCommandHelper.AddTestMedia(sourcePath, sourcePath, data);

                // arrange - read command
                var cancellationTokenSource = new CancellationTokenSource();
                var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
                    Enumerable.Empty<IPhysicalDrive>(), sourcePath, destinationPath, new Size(0, Unit.Bytes), 0, false,
                    false, 0);

                // act - read source physical drive to zip compressed img
                var result = await readCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - data written is identical to uncompressed source bytes
                var destinationBytes = await testCommandHelper.ReadMediaData(destinationPath);
                Assert.True(HasZipMagicNumber(destinationBytes));
                Assert.True(data.Length > destinationBytes.Length);

                var uncompressedDestData = await UncompressZipData(destinationBytes);
                Assert.Equal(data.Length, uncompressedDestData.Length);
                Assert.Equal(data, uncompressedDestData);
            }
            finally
            {
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
            }
        }

        [Fact]
        public async Task WhenReadPhysicalDriveToGZipCompressedImgThenDataIsIdentical()
        {
            var data = TestDataHelper.CreateTestData(10.MB());
            var sourcePath = TestCommandHelper.PhysicalDrivePath;
            var destinationPath = $"{Guid.NewGuid()}.img.gz";

            try
            {
                var testCommandHelper = new TestCommandHelper();

                // arrange - create source physical drive
                await testCommandHelper.AddTestMedia(sourcePath, sourcePath, data);

                // arrange - read command
                var cancellationTokenSource = new CancellationTokenSource();
                var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
                    Enumerable.Empty<IPhysicalDrive>(), sourcePath, destinationPath, new Size(0, Unit.Bytes), 0, false,
                    false, 0);

                // act - read source physical drive to gzip compressed img
                var result = await readCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // assert - data written is identical to uncompressed source bytes
                var destinationBytes = await testCommandHelper.ReadMediaData(destinationPath);
                Assert.True(HasGZipMagicNumber(destinationBytes));
                Assert.True(data.Length > destinationBytes.Length);

                var uncompressedDestData = await UncompressGZipData(destinationBytes);
                Assert.Equal(data.Length, uncompressedDestData.Length);
                Assert.Equal(data, uncompressedDestData);
            }
            finally
            {
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
            }
        }

        private static bool HasZipMagicNumber(byte[] data) =>
            MagicBytes.HasMagicNumber(MagicBytes.ZipMagicNumber1, data, 0) ||
            MagicBytes.HasMagicNumber(MagicBytes.ZipMagicNumber2, data, 0) ||
            MagicBytes.HasMagicNumber(MagicBytes.ZipMagicNumber3, data, 0);

        private static bool HasGZipMagicNumber(byte[] data) =>
            MagicBytes.HasMagicNumber(MagicBytes.GzHeader, data, 0);

        private static async Task<byte[]> UncompressZipData(byte[] data)
        {
            await using var uncompressedStream = new MemoryStream();
            await using var compressedStream = new MemoryStream(data);
            using var zipArchive = new ZipArchive(compressedStream);
            var zipEntry = zipArchive.Entries.FirstOrDefault();
            if (zipEntry == null)
            {
                return Array.Empty<byte>();
            }

            await using var zipEntryStream = zipEntry.Open();

            var buffer = new byte[1024 * 1024];
            int bytesRead;
            do
            {
                bytesRead = await zipEntryStream.ReadAsync(buffer, 0, buffer.Length);
                await uncompressedStream.WriteAsync(buffer, 0, bytesRead);
            } while (bytesRead > 0);

            return uncompressedStream.ToArray();
        }

        private static async Task<byte[]> UncompressGZipData(byte[] data)
        {
            await using var uncompressedStream = new MemoryStream();
            await using var compressedStream = new MemoryStream(data);

            await using var gZipStream = new GZipStream(compressedStream, CompressionMode.Decompress);

            var buffer = new byte[1024 * 1024];
            int bytesRead;
            do
            {
                bytesRead = await gZipStream.ReadAsync(buffer, 0, buffer.Length);
                await uncompressedStream.WriteAsync(buffer, 0, bytesRead);
            } while (bytesRead > 0);

            return uncompressedStream.ToArray();
        }
    }
}