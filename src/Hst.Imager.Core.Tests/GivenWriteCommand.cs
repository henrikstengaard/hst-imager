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
    }
}