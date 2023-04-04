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
        public async Task WhenReadSourceToImgDestinationThenReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var fakeCommandHelper = new TestCommandHelper();
            fakeCommandHelper.AddTestMedia(sourcePath, ImageSize);
            var cancellationTokenSource = new CancellationTokenSource();

            // act - read source img to destination img
            var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), fakeCommandHelper,
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
            var sourceBytes = await fakeCommandHelper.ReadMediaData(sourcePath);
            var destinationBytes = await fakeCommandHelper.ReadMediaData(destinationPath);
            Assert.Equal(sourceBytes, destinationBytes);
        }

        [Fact]
        public async Task WhenReadSourceToImgDestinationWithSizeThenReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var size = 16 * 512;
            var fakeCommandHelper = new TestCommandHelper();
            fakeCommandHelper.AddTestMedia(sourcePath, ImageSize);
            var cancellationTokenSource = new CancellationTokenSource();

            // act - read source img to destination img
            var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(size, Unit.Bytes), 0, false, false, 0);
            var result = await readCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert data is identical
            var sourceBytes = (await fakeCommandHelper.ReadMediaData(sourcePath)).Take(size).ToArray();
            Assert.Equal(size, sourceBytes.Length);
            var destinationBytes = await fakeCommandHelper.ReadMediaData(destinationPath);
            Assert.Equal(sourceBytes, destinationBytes);
        }

        [Fact]
        public async Task WhenReadSourceToImgDestination2TimesThenReadDataIsIdentical2()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.img";
            var size = 16 * 512;
            var fakeCommandHelper = new TestCommandHelper();
            fakeCommandHelper.AddTestMedia(sourcePath, size);
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - read command
            var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(size, Unit.Bytes), 0, false, false, 0);

            // act - read source img to destination img 1st time
            var result = await readCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);
            
            // act - read source img to destination img 2nd time
            result = await readCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);
            
            // assert data is identical
            var sourceBytes = await fakeCommandHelper.ReadMediaData(sourcePath);
            Assert.Equal(size, sourceBytes.Length);
            var destinationBytes = await fakeCommandHelper.ReadMediaData(destinationPath);
            Assert.Equal(sourceBytes, destinationBytes);
        }

        [Fact]
        public async Task WhenReadSourceToVhdDestination2TimesThenReadDataIsIdentical2()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.vhd";
            var size = 16 * 512;
            var fakeCommandHelper = new TestCommandHelper();
            fakeCommandHelper.AddTestMedia(sourcePath, size);
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - read command
            var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(size, Unit.Bytes), 0, false, false, 0);

            // act - read source img to destination img 1st time
            var result = await readCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);
            
            // act - read source img to destination img 2nd time
            result = await readCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);
            
            // assert data is identical
            var sourceBytes = await fakeCommandHelper.ReadMediaData(sourcePath);
            Assert.Equal(size, sourceBytes.Length);
            var destinationBytes = await fakeCommandHelper.ReadMediaData(destinationPath);
            Assert.Equal(sourceBytes, destinationBytes);
        }
        
        [Fact]
        public async Task WhenReadSourceToVhdDestinationThenReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.vhd";
            var fakeCommandHelper = new TestCommandHelper();
            fakeCommandHelper.AddTestMedia(sourcePath, ImageSize);
            var cancellationTokenSource = new CancellationTokenSource();

            // assert: destination path vhd doesn't exist
            Assert.False(File.Exists(destinationPath));

            // act: read source img to destination vhd
            var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), fakeCommandHelper,
                Enumerable.Empty<IPhysicalDrive>(), sourcePath, destinationPath, new Size(), 0, false, false, 0);
            var result = await readCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // get source bytes
            var sourceBytes = await fakeCommandHelper.ReadMediaData(sourcePath);

            // get destination bytes from vhd
            var destinationBytes =
                await ReadMediaBytes(fakeCommandHelper, destinationPath, ImageSize);
            var destinationPathSize = new FileInfo(destinationPath).Length;

            // assert length is not the same (vhd file format different than img) and bytes are the same
            Assert.NotEqual(sourceBytes.Length, destinationPathSize);
            Assert.Equal(sourceBytes, destinationBytes);

            // delete destination path vhd
            File.Delete(destinationPath);
        }

        [Fact]
        public async Task WhenReadSourceToVhdDestinationWithSizeThenReadDataIsIdentical()
        {
            // arrange
            var sourcePath = $"{Guid.NewGuid()}.img";
            var destinationPath = $"{Guid.NewGuid()}.vhd";
            var size = 16 * 512;
            var fakeCommandHelper = new TestCommandHelper();
            fakeCommandHelper.AddTestMedia(sourcePath, ImageSize);
            var cancellationTokenSource = new CancellationTokenSource();

            // act - read source img to destination vhd
            var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), fakeCommandHelper,
                Enumerable.Empty<IPhysicalDrive>(), sourcePath, destinationPath, new Size(size, Unit.Bytes), 0, false, false, 0);
            var result = await readCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // get source bytes
            var sourceBytes = (await fakeCommandHelper.ReadMediaData(sourcePath)).Take(size).ToArray();
            Assert.Equal(size, sourceBytes.Length);

            // get destination bytes from vhd
            var destinationBytes = await fakeCommandHelper.ReadMediaData(destinationPath);
            var destinationPathSize = new FileInfo(destinationPath).Length;

            // assert length is not the same (vhd file format different than img) and bytes are the same
            Assert.NotEqual(sourceBytes.Length, destinationPathSize);
            Assert.Equal(sourceBytes, destinationBytes);

            // delete destination path vhd
            File.Delete(destinationPath);
        }
    }
}