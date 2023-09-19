using System.IO.Compression;
using System.Linq;
using Hst.Imager.Core.Extensions;

namespace Hst.Imager.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Core;
    using Commands;
    using Models;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class GivenWriteCommand : CommandTestBase
    {
        [Fact]
        public async Task WhenWriteImgToPhysicalDriveThenWrittenDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = TestCommandHelper.PhysicalDrivePath;
            var testCommandHelper = new TestCommandHelper();
            
            // arrange - create source img media
            testCommandHelper.AddTestMedia(sourcePath, ImageSize);

            // arrange - create destination physical drive
            testCommandHelper.AddTestMedia(destinationPath);

            // act - write img media to physical drive
            var cancellationTokenSource = new CancellationTokenSource();
            var writeCommand =
                new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
                    sourcePath, destinationPath, new Size(), 0, false, false);
            DataProcessedEventArgs dataProcessedEventArgs = null;
            writeCommand.DataProcessed += (_, args) => { dataProcessedEventArgs = args; };
            var result = await writeCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            Assert.NotNull(dataProcessedEventArgs);
            Assert.NotEqual(0, dataProcessedEventArgs.PercentComplete);
            Assert.NotEqual(0, dataProcessedEventArgs.BytesProcessed);
            Assert.Equal(0, dataProcessedEventArgs.BytesRemaining);
            Assert.NotEqual(0, dataProcessedEventArgs.BytesTotal);

            // assert data is identical
            var sourceBytes = testCommandHelper.GetTestMedia(sourcePath).Data;
            var destinationBytes = testCommandHelper.GetTestMedia(destinationPath).Data;
            Assert.Equal(sourceBytes, destinationBytes);
        }

        [Fact]
        public async Task WhenWriteVhdToPhysicalDriveThenWrittenDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.vhd";
            var destinationPath = TestCommandHelper.PhysicalDrivePath;
            var testCommandHelper = new TestCommandHelper();
            var data = testCommandHelper.CreateTestData(ImageSize);
            
            try
            {
                // arrange - create source vhd media
                await testCommandHelper.WriteMediaData(sourcePath, data);
                
                // arrange - create destination physical drive
                testCommandHelper.AddTestMedia(destinationPath);

                // act - write vhd media to physical drive
                var cancellationTokenSource = new CancellationTokenSource();
                var writeCommand = new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(data.Length, Unit.Bytes), 0, false, false);
                var result = await writeCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);
            }
            finally
            {
                if (File.Exists(sourcePath))
                {
                    File.Delete(sourcePath);
                }
            }
        }
        
        [Fact]
        public async Task WhenWriteImgXzToPhysicalDriveThenWrittenDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img.xz";
            var destinationPath = TestCommandHelper.PhysicalDrivePath;
            var testCommandHelper = new TestCommandHelper();
            
            try
            {
                // arrange - create source img xz compressed media
                File.Copy(Path.Combine("TestData", "Xz", "test.txt.xz"), sourcePath);
                
                // arrange - create destination physical drive
                testCommandHelper.AddTestMedia(destinationPath);

                // act - write img media to physical drive
                var cancellationTokenSource = new CancellationTokenSource();
                var writeCommand =
                    new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
                        sourcePath, destinationPath, new Size(), 0, false, false);
                DataProcessedEventArgs dataProcessedEventArgs = null;
                writeCommand.DataProcessed += (_, args) => { dataProcessedEventArgs = args; };
                var result = await writeCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // arrange - get source bytes uncompressed
                var sourceBytes = await File.ReadAllBytesAsync(Path.Combine("TestData", "Xz", "test.txt"), cancellationTokenSource.Token);

                // assert - data processed is 100 percent complete and has processed all uncompressed 
                Assert.NotNull(dataProcessedEventArgs);
                Assert.Equal(100, dataProcessedEventArgs.PercentComplete);
                Assert.Equal(sourceBytes.Length, dataProcessedEventArgs.BytesProcessed);
                Assert.Equal(0, dataProcessedEventArgs.BytesRemaining);
                Assert.Equal(sourceBytes.Length, dataProcessedEventArgs.BytesTotal);

                // assert - data written is identical to uncompressed source bytes
                var destinationBytes = testCommandHelper.GetTestMedia(destinationPath).Data;
                Assert.Equal(sourceBytes, destinationBytes);
            }
            finally
            {
                if (File.Exists(sourcePath))
                {
                    File.Delete(sourcePath);
                }
            }
        }
        
        [Fact]
        public async Task WhenWriteRarCompressedImgToPhysicalDriveThenWrittenDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img.rar";
            var destinationPath = TestCommandHelper.PhysicalDrivePath;
            var testCommandHelper = new TestCommandHelper();
            
            try
            {
                // arrange - create source img rar compressed media
                File.Copy(Path.Combine("TestData", "compressed-images", "1gb.img.rar"), sourcePath);
                
                // arrange - create destination physical drive as empty file
                await File.WriteAllBytesAsync(TestCommandHelper.PhysicalDrivePath, Array.Empty<byte>());

                // act - write img media to physical drive
                var cancellationTokenSource = new CancellationTokenSource();
                var writeCommand =
                    new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
                        sourcePath, destinationPath, new Size(), 0, false, false);
                DataProcessedEventArgs dataProcessedEventArgs = null;
                writeCommand.DataProcessed += (_, args) => { dataProcessedEventArgs = args; };
                var result = await writeCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);
                
                // assert - data processed is 100 percent complete and has processed all uncompressed 
                Assert.NotNull(dataProcessedEventArgs);
                Assert.Equal(100, dataProcessedEventArgs.PercentComplete);
                Assert.Equal(1020055040, dataProcessedEventArgs.BytesProcessed);
                Assert.Equal(0, dataProcessedEventArgs.BytesRemaining);
                Assert.Equal(1020055040, dataProcessedEventArgs.BytesTotal);

                // assert - data written to physical drive file is identical to uncompressed img
                await AssertPathIsIdenticalToUncompressedImg(destinationPath);
            }
            finally
            {
                if (File.Exists(sourcePath))
                {
                    File.Delete(sourcePath);
                }
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
            }
        }

        [Fact]
        public async Task WhenWriteZipCompressedImgToPhysicalDriveThenWrittenDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img.zip";
            var destinationPath = TestCommandHelper.PhysicalDrivePath;
            var testCommandHelper = new TestCommandHelper();
            
            try
            {
                // arrange - create source zip compressed img media
                File.Copy(Path.Combine("TestData", "compressed-images", "1gb.img.zip"), sourcePath);
                
                // arrange - create destination physical drive as empty file
                await File.WriteAllBytesAsync(TestCommandHelper.PhysicalDrivePath, Array.Empty<byte>());

                // act - write zip compressed img media to physical drive
                var cancellationTokenSource = new CancellationTokenSource();
                var writeCommand =
                    new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
                        sourcePath, destinationPath, new Size(), 0, false, false);
                DataProcessedEventArgs dataProcessedEventArgs = null;
                writeCommand.DataProcessed += (_, args) => { dataProcessedEventArgs = args; };
                var result = await writeCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);
                
                // assert - data processed is 100 percent complete and has processed all uncompressed 
                Assert.NotNull(dataProcessedEventArgs);
                Assert.Equal(100, dataProcessedEventArgs.PercentComplete);
                Assert.Equal(1020055040, dataProcessedEventArgs.BytesProcessed);
                Assert.Equal(0, dataProcessedEventArgs.BytesRemaining);
                Assert.Equal(1020055040, dataProcessedEventArgs.BytesTotal);

                // assert - data written to physical drive file is identical to uncompressed img
                await AssertPathIsIdenticalToUncompressedImg(destinationPath);
            }
            finally
            {
                if (File.Exists(sourcePath))
                {
                    File.Delete(sourcePath);
                }
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
            }
        }

        [Fact]
        public async Task WhenWriteGZipCompressedImgToPhysicalDriveThenWrittenDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img.gz";
            var destinationPath = TestCommandHelper.PhysicalDrivePath;
            var testCommandHelper = new TestCommandHelper();
            
            try
            {
                // arrange - create source gzip compressed img media
                File.Copy(Path.Combine("TestData", "compressed-images", "1gb.img.gz"), sourcePath);
                
                // arrange - create destination physical drive as empty file
                await File.WriteAllBytesAsync(TestCommandHelper.PhysicalDrivePath, Array.Empty<byte>());

                // act - write gzip compressed img media to physical drive
                var cancellationTokenSource = new CancellationTokenSource();
                var writeCommand =
                    new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
                        sourcePath, destinationPath, new Size(), 0, false, false);
                DataProcessedEventArgs dataProcessedEventArgs = null;
                writeCommand.DataProcessed += (_, args) => { dataProcessedEventArgs = args; };
                var result = await writeCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);
                
                // assert - data processed is 100 percent complete and has processed all uncompressed 
                Assert.NotNull(dataProcessedEventArgs);
                Assert.Equal(100, dataProcessedEventArgs.PercentComplete);
                Assert.Equal(1020055040, dataProcessedEventArgs.BytesProcessed);
                Assert.Equal(0, dataProcessedEventArgs.BytesRemaining);
                Assert.Equal(1020055040, dataProcessedEventArgs.BytesTotal);

                // assert - data written to physical drive file is identical to uncompressed img
                await AssertPathIsIdenticalToUncompressedImg(destinationPath);
            }
            finally
            {
                if (File.Exists(sourcePath))
                {
                    File.Delete(sourcePath);
                }
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
            }
        }

        [Fact]
        public async Task WhenWriteXzCompressedImgToPhysicalDriveThenWrittenDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img.xz";
            var destinationPath = TestCommandHelper.PhysicalDrivePath;
            var testCommandHelper = new TestCommandHelper();
            
            try
            {
                // arrange - create source xz compressed img media
                File.Copy(Path.Combine("TestData", "compressed-images", "1gb.img.xz"), sourcePath);
                
                // arrange - create destination physical drive as empty file
                await File.WriteAllBytesAsync(TestCommandHelper.PhysicalDrivePath, Array.Empty<byte>());

                // act - write xz compressed img media to physical drive
                var cancellationTokenSource = new CancellationTokenSource();
                var writeCommand =
                    new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
                        sourcePath, destinationPath, new Size(), 0, false, false);
                DataProcessedEventArgs dataProcessedEventArgs = null;
                writeCommand.DataProcessed += (_, args) => { dataProcessedEventArgs = args; };
                var result = await writeCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);
                
                // assert - data processed is 100 percent complete and has processed all uncompressed 
                Assert.NotNull(dataProcessedEventArgs);
                Assert.Equal(100, dataProcessedEventArgs.PercentComplete);
                Assert.Equal(1020055040, dataProcessedEventArgs.BytesProcessed);
                Assert.Equal(0, dataProcessedEventArgs.BytesRemaining);
                Assert.Equal(1020055040, dataProcessedEventArgs.BytesTotal);

                // assert - data written to physical drive file is identical to uncompressed img
                await AssertPathIsIdenticalToUncompressedImg(destinationPath);
            }
            finally
            {
                if (File.Exists(sourcePath))
                {
                    File.Delete(sourcePath);
                }
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
            }
        }
        
        private static async Task AssertPathIsIdenticalToUncompressedImg(string path)
        {
            // open zip compressed img path as source stream
            var zipCompressedImgPath = Path.Combine("TestData", "compressed-images", "1gb.img.zip");
            using var zipArchive =
                new ZipArchive(File.OpenRead(zipCompressedImgPath), ZipArchiveMode.Read);
            var zipEntry = zipArchive.Entries.FirstOrDefault();
            if (zipEntry == null)
            {
                throw new ArgumentException("Zip compressed img doesn't contain an entry", nameof(zipCompressedImgPath));
            }
            await using var srcStream = zipEntry.Open();
            
            // open path as destination stream
            await using var destStream = File.OpenRead(path);

            var srcBuffer = new byte[1024 * 1024];
            var destBuffer = new byte[1024 * 1024];
            int srcBytesRead;
            int destBytesRead;
            do
            {
                srcBytesRead = await srcStream.FillAsync(srcBuffer, 0, srcBuffer.Length);
                destBytesRead = await destStream.ReadAsync(destBuffer, 0, destBuffer.Length);

                // assert - source bytes read is equal destination bytes read
                Assert.Equal(srcBytesRead, destBytesRead);
                
                // assert - source buffer is equal to destination buffer
                Assert.True(srcBuffer.SequenceEqual(destBuffer));
            } while (srcBytesRead > 0 && srcBytesRead == destBytesRead);
        }
    }
}