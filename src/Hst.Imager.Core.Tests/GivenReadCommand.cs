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
        public async Task WhenReadPhysicalDriveTToImgThenReadDataIsIdentical()
        {
            // arrange - paths and test command helper
            var sourcePath = TestCommandHelper.PhysicalDrivePath;
            var destinationPath = $"{Guid.NewGuid()}.img";
            var testCommandHelper = new TestCommandHelper();

            // arrange - create source physical drive
            testCommandHelper.AddTestMedia(sourcePath, ImageSize);

            // arrange - create destination img
            testCommandHelper.AddTestMedia(destinationPath);

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
            testCommandHelper.AddTestMedia(sourcePath, ImageSize);

            // arrange - create destination img
            testCommandHelper.AddTestMedia(destinationPath);
            
            // act - read source physical drive to destination img
            var cancellationTokenSource = new CancellationTokenSource();
            var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(size, Unit.Bytes), 0, false, false, 0);
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
            testCommandHelper.AddTestMedia(sourcePath, ImageSize);

            // arrange - create destination img
            testCommandHelper.AddTestMedia(destinationPath);
            
            // arrange - read command
            var cancellationTokenSource = new CancellationTokenSource();
            var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(firstReadSize, Unit.Bytes), 0, false, false, 0);

            // act - read source physical drive to destination img 1st time
            var result = await readCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - data is identical
            var sourceBytes = (await testCommandHelper.ReadMediaData(sourcePath)).Take(firstReadSize).ToArray();
            Assert.Equal(firstReadSize, sourceBytes.Length);
            var destinationBytes = await testCommandHelper.ReadMediaData(destinationPath);
            Assert.Equal(sourceBytes, destinationBytes);
            
            readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(secondReadSize, Unit.Bytes), 0, false, false, 0);
            
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
                testCommandHelper.AddTestMedia(sourcePath, ImageSize);

                // arrange - read command read 8192 bytes
                var cancellationTokenSource = new CancellationTokenSource();
                var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
                    new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(firstReadSize, Unit.Bytes), 0, false, false, 0);

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
                    new List<IPhysicalDrive>(), sourcePath, destinationPath, new Size(secondReadSize, Unit.Bytes), 0, false, false, 0);
            
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
                testCommandHelper.AddTestMedia(sourcePath, ImageSize);

                // arrange - read command
                var cancellationTokenSource = new CancellationTokenSource();
                var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), testCommandHelper,
                    Enumerable.Empty<IPhysicalDrive>(), sourcePath, destinationPath, new Size(), 0, false, false, 0);

                // act - read source physical drive to destination vhd
                var result = await readCommand.Execute(cancellationTokenSource.Token);
                Assert.True(result.IsSuccess);

                // get source bytes
                var sourceBytes = await testCommandHelper.ReadMediaData(sourcePath);

                // get destination bytes from vhd
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
            var fakeCommandHelper = new TestCommandHelper();
            
            try
            {
                // arrange - create source physical drive
                fakeCommandHelper.AddTestMedia(sourcePath, ImageSize);

                // arrange - read command
                var cancellationTokenSource = new CancellationTokenSource();
                var readCommand = new ReadCommand(new NullLogger<ReadCommand>(), fakeCommandHelper,
                    Enumerable.Empty<IPhysicalDrive>(), sourcePath, destinationPath, new Size(size, Unit.Bytes), 0, false, false, 0);

                // act - read source physical drive to destination vhd
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
            }
            finally
            {
                if (File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                }
            }
        }
    }
}