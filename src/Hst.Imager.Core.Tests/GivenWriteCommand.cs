using System.IO.Compression;
using System.Linq;
using Hst.Core.Extensions;
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
    using System.Text;

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
            testCommandHelper.AddTestMediaWithData(sourcePath, ImageSize);

            // arrange - create destination physical drive
            testCommandHelper.AddTestMedia(destinationPath, 1.MB());

            // act - write img media to physical drive
            var cancellationTokenSource = new CancellationTokenSource();
            var writeCommand =
                new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
                    sourcePath, destinationPath, new Size(), 0, false, false, false);
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
            var sourceBytes = await testCommandHelper.GetTestMedia(sourcePath).ReadData();
            var destinationBytes = await testCommandHelper.GetTestMedia(destinationPath).ReadData();
            Assert.True(sourceBytes.SequenceEqual(destinationBytes.Take(ImageSize)));
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
                testCommandHelper.AddTestMedia(destinationPath, 1.MB());

                // act - write vhd media to physical drive
                var cancellationTokenSource = new CancellationTokenSource();
                var writeCommand = new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(), sourcePath, destinationPath,
                    new Size(data.Length, Unit.Bytes), 0, false, false, false);
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
                testCommandHelper.AddTestMedia(destinationPath, 1.MB());

                // act - write img media to physical drive
                var cancellationTokenSource = new CancellationTokenSource();
                var writeCommand =
                    new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
                        sourcePath, destinationPath, new Size(), 0, false, false, false);
                DataProcessedEventArgs dataProcessedEventArgs = null;
                writeCommand.DataProcessed += (_, args) => { dataProcessedEventArgs = args; };
                var result = await writeCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // arrange - get source bytes uncompressed
                var sourceText = await File.ReadAllTextAsync(Path.Combine("TestData", "Xz", "test.txt"), cancellationTokenSource.Token);
                if (OperatingSystem.IsWindows())
                {
                    sourceText = sourceText.Replace("\r\n", "\n");
                }
                var sourceBytes = Encoding.UTF8.GetBytes(sourceText);

                // assert - data processed is 100 percent complete and has processed all uncompressed 
                Assert.NotNull(dataProcessedEventArgs);
                Assert.Equal(100, dataProcessedEventArgs.PercentComplete);
                Assert.Equal(sourceBytes.Length, dataProcessedEventArgs.BytesProcessed);
                Assert.Equal(0, dataProcessedEventArgs.BytesRemaining);
                Assert.Equal(sourceBytes.Length, dataProcessedEventArgs.BytesTotal);

                // assert - data written is identical to uncompressed source bytes
                var destinationStream = testCommandHelper.GetTestMedia(destinationPath).Stream;
                destinationStream.Position = 0;
                var destinationBytes = await destinationStream.ReadBytes(sourceBytes.Length);
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
                
                // arrange - create destination physical drive of 16gb
                testCommandHelper.AddTestMedia(destinationPath, 16.GB());

                // act - write img media to physical drive
                var cancellationTokenSource = new CancellationTokenSource();
                var writeCommand =
                    new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
                        sourcePath, destinationPath, new Size(), 0, false, false, false);
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
                var destMedia = testCommandHelper.GetTestMedia(destinationPath);
                await AssertTestMediaIsIdenticalToUncompressedImg(destMedia);
            }
            finally
            {
                TestHelper.DeletePaths(sourcePath);
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
                
                // arrange - create destination physical drive of 16gb
                testCommandHelper.AddTestMedia(destinationPath, 16.GB());

                // act - write zip compressed img media to physical drive
                var cancellationTokenSource = new CancellationTokenSource();
                var writeCommand =
                    new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
                        sourcePath, destinationPath, new Size(), 0, false, false, false);
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
                var destMedia = testCommandHelper.GetTestMedia(destinationPath);
                await AssertTestMediaIsIdenticalToUncompressedImg(destMedia);
            }
            finally
            {
                TestHelper.DeletePaths(sourcePath);
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
                
                // arrange - create destination physical drive of 16gb
                testCommandHelper.AddTestMedia(destinationPath, 16.GB());

                // act - write gzip compressed img media to physical drive
                var cancellationTokenSource = new CancellationTokenSource();
                var writeCommand =
                    new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
                        sourcePath, destinationPath, new Size(), 0, false, false, false);
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
                var destMedia = testCommandHelper.GetTestMedia(destinationPath);
                await AssertTestMediaIsIdenticalToUncompressedImg(destMedia);
            }
            finally
            {
                TestHelper.DeletePaths(sourcePath);
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
                
                // arrange - create destination physical drive of 16gb
                testCommandHelper.AddTestMedia(destinationPath, 16.GB());

                // act - write xz compressed img media to physical drive
                var cancellationTokenSource = new CancellationTokenSource();
                var writeCommand =
                    new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
                        sourcePath, destinationPath, new Size(), 0, false, false, false);
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
                var destMedia = testCommandHelper.GetTestMedia(destinationPath);
                await AssertTestMediaIsIdenticalToUncompressedImg(destMedia);
            }
            finally
            {
                TestHelper.DeletePaths(sourcePath);
            }
        }

        [Fact]
        public async Task When_WriteSrcImgToDestPhysicalDrive_Then_SrcAndDestAreIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = TestCommandHelper.PhysicalDrivePath;
            var testCommandHelper = new TestCommandHelper();
            var imageSize = 50.MB();
            const bool skipZeroFilled = false;

            // arrange - create source image bytes
            var sourceImageBytes = new byte[imageSize];
            Array.Fill<byte>(sourceImageBytes, 2, 0, 512);

            // arrange - create destination physical drive bytes
            var destinationPhysicalDriveBytes = new byte[imageSize];
            Array.Fill<byte>(destinationPhysicalDriveBytes, 1);
            
            // arrange - create source image media
            await testCommandHelper.AddTestMedia(sourcePath, sourcePath, sourceImageBytes);

            // arrange - create destination physical drive
            await testCommandHelper.AddTestMedia(destinationPath, destinationPath, destinationPhysicalDriveBytes);

            // act - write source image media to destination physical drive
            var cancellationTokenSource = new CancellationTokenSource();
            var writeCommand =
                new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
                    sourcePath, destinationPath, new Size(), 0, false, false, skipZeroFilled);
            var dataProcessedEventArgs = new List<DataProcessedEventArgs>();
            writeCommand.DataProcessed += (_, args) => { dataProcessedEventArgs.Add(args); };
            var result = await writeCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - data processed is not empty and has more than 10 data processed events
            Assert.NotEmpty(dataProcessedEventArgs);
            Assert.True(dataProcessedEventArgs.Count > 10);
            
            // arrange - get actual destination bytes
            var actualDestinationBytes = await testCommandHelper.GetTestMedia(destinationPath).ReadData();
            
            // assert - data written to physical drive file is identical to source image bytes
            Assert.True(sourceImageBytes.SequenceEqual(actualDestinationBytes));
        }

        [Fact]
        public async Task When_WriteSrcImgToDestPhysicalDriveSkippingZeroFilled_Then_OnlyUsedSectorsAreWritten()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = TestCommandHelper.PhysicalDrivePath;
            var testCommandHelper = new TestCommandHelper();
            var imageSize = 50.MB();
            const bool skipZeroFilled = true;
            
            // arrange - create source image bytes
            var sourceImageBytes = new byte[imageSize];
            Array.Fill<byte>(sourceImageBytes, 2, 0, 512);

            // arrange - create destination physical drive bytes
            var destinationPhysicalDriveBytes = new byte[imageSize];
            Array.Fill<byte>(destinationPhysicalDriveBytes, 1);
            
            // arrange - create source image media
            await testCommandHelper.AddTestMedia(sourcePath, sourcePath, sourceImageBytes);

            // arrange - create destination physical drive
            await testCommandHelper.AddTestMedia(destinationPath, destinationPath, destinationPhysicalDriveBytes);

            // act - write source image media to destination physical drive
            var cancellationTokenSource = new CancellationTokenSource();
            var writeCommand =
                new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
                    sourcePath, destinationPath, new Size(), 0, false, false, skipZeroFilled);
            var dataProcessedEventArgs = new List<DataProcessedEventArgs>();
            writeCommand.DataProcessed += (_, args) => { dataProcessedEventArgs.Add(args); };
            var result = await writeCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - data processed is not empty and has more than 10 data processed events
            Assert.NotEmpty(dataProcessedEventArgs);
            Assert.True(dataProcessedEventArgs.Count > 10);
            
            // arrange - create expected destination bytes with 512 first bytes from source image file
            // and remaining bytes from destination physical drive
            var expectedDestinationBytes = new byte[imageSize];
            Array.Copy(destinationPhysicalDriveBytes, 0, expectedDestinationBytes, 0, destinationPhysicalDriveBytes.Length);
            Array.Copy(sourceImageBytes, 0, expectedDestinationBytes, 0, 512);

            // arrange - get actual destination bytes
            var actualDestinationBytes = await testCommandHelper.GetTestMedia(destinationPath).ReadData();
            
            // assert - data written to physical drive file is identical to expected destination bytes
            Assert.True(expectedDestinationBytes.SequenceEqual(actualDestinationBytes));
        }

        private static async Task AssertTestMediaIsIdenticalToUncompressedImg(TestMedia testMedia)
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
            await using var destStream = testMedia.Stream;
            destStream.Position = 0;

            var srcBuffer = new byte[1024 * 1024];
            var destBuffer = new byte[1024 * 1024];
            int srcBytesRead;
            do
            {
                srcBytesRead = await srcStream.FillAsync(srcBuffer, 0, srcBuffer.Length);
                var destBytesRead = await destStream.ReadAsync(destBuffer, 0, destBuffer.Length);

                // assert - source bytes read is equal destination bytes read
                if (srcBytesRead == destBytesRead)
                {
                    Assert.True(srcBuffer.SequenceEqual(destBuffer));
                    continue;
                }
                Assert.True(srcBuffer.Take(srcBytesRead).SequenceEqual(destBuffer.Take(srcBytesRead)));
            } while (srcBytesRead > 0);
        }

        [Fact]
        public async Task WhenWriteSizeIsLargerThanPhysicalDriveThenErrorIsReturned()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = TestCommandHelper.PhysicalDrivePath;
            var testCommandHelper = new TestCommandHelper();
            
            // arrange - create source img media
            testCommandHelper.AddTestMediaWithData(sourcePath, 10.MB());

            // arrange - create destination physical drive
            testCommandHelper.AddTestMediaWithData(destinationPath, 1.MB());

            // act - write img media to physical drive
            var cancellationTokenSource = new CancellationTokenSource();
            var writeCommand =
                new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper, new List<IPhysicalDrive>(),
                    sourcePath, destinationPath, new Size(), 0, false, false, false);
            var result = await writeCommand.Execute(cancellationTokenSource.Token);
            
            // assert - write failed and returned write size too large error
            Assert.False(result.IsSuccess);
            Assert.IsType<WriteSizeTooLargeError>(result.Error);
        }
    }
}