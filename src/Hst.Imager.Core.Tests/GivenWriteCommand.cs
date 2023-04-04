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
        public async Task WhenWriteSourceToImgDestinationThenReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var testCommandHelper = new TestCommandHelper();
            testCommandHelper.AddTestMedia(sourcePath, ImageSize);
            testCommandHelper.AddTestMedia(destinationPath);
            var cancellationTokenSource = new CancellationTokenSource();

            // act - write source img to destination img
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
        public async Task WhenWriteSourceToVhdDestinationThenReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.vhd";
            var testCommandHelper = new TestCommandHelper();
            testCommandHelper.AddTestMedia(sourcePath, ImageSize);
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange destination vhd has copy of source img data
            var sourceBytes = testCommandHelper.GetTestMedia(sourcePath).Data;

            // act - write source img to destination vhd
            var writeCommand = new WriteCommand(new NullLogger<WriteCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(sourceBytes.Length, Unit.Bytes), 0, false, false);
            var result = await writeCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // delete destination path vhd
            File.Delete(destinationPath);
        }
    }
}