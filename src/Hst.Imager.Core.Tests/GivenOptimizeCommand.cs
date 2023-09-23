namespace Hst.Imager.Core.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Commands;
    using Models;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class GivenOptimizeCommand : CommandTestBase
    {
        [Fact]
        public async Task WhenOptimizeImgWithSizeSetThenSizeIsChanged()
        {
            // arrange - path, size and test command helper
            var imgPath = $"{Guid.NewGuid()}.img";
            var size = 8192;
            var testCommandHelper = new TestCommandHelper();

            // arrange - create img media
            testCommandHelper.AddTestMediaWithData(imgPath, ImageSize);
            
            // arrange - optimize command
            var cancellationTokenSource = new CancellationTokenSource();
            var optimizeCommand = new OptimizeCommand(new NullLogger<OptimizeCommand>(), testCommandHelper, imgPath,
                new Models.Size(size, Unit.Bytes), PartitionTable.None);

            // act - optimize img media
            var result = await optimizeCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert media contains optimized rigid disk block size
            var optimizedBytes = await testCommandHelper.ReadMediaData(imgPath);
            Assert.Equal(size, optimizedBytes.Length);
        }
        
        [Fact]
        public async Task WhenOptimizeImgWithoutSizeSetThenResultIsFaulted()
        {
            // arrange - path and test command helper
            var imgPath = $"{Guid.NewGuid()}.img";
            var testCommandHelper = new TestCommandHelper();

            // arrange - create img media
            testCommandHelper.AddTestMediaWithData(imgPath, ImageSize);

            // arrange - optimize command
            var cancellationTokenSource = new CancellationTokenSource();
            var optimizeCommand = new OptimizeCommand(new NullLogger<OptimizeCommand>(), testCommandHelper, imgPath,
                new Models.Size(0, Unit.Bytes), PartitionTable.None);

            // act - optimize img media
            var result = await optimizeCommand.Execute(cancellationTokenSource.Token);
            
            // assert - optimize failed (size error)
            Assert.True(result.IsFaulted);
        }

        [Fact]
        public async Task WhenOptimizeImgWithRdbTrueSetThenSizeIsChangedToRdbSize()
        {
            // arrange - path, size and test command helper
            var imgPath = $"{Guid.NewGuid()}.img";
            var rigidDiskBlockSize = 8192;
            var testCommandHelper = new TestCommandHelper();
            
            // arrange - create img media
            await testCommandHelper.AddTestMedia(imgPath, data: await File.ReadAllBytesAsync(
                Path.Combine("TestData", "rigid-disk-block.img")));

            // arrange - optimize command
            var cancellationTokenSource = new CancellationTokenSource();
            var optimizeCommand = new OptimizeCommand(new NullLogger<OptimizeCommand>(), testCommandHelper, imgPath,
                new Models.Size(rigidDiskBlockSize, Unit.Bytes), PartitionTable.Rdb);

            // act - optimize img media
            var result = await optimizeCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert media contains optimized rigid disk block size
            var optimizedBytes = testCommandHelper.GetTestMedia(imgPath);
            Assert.Equal(rigidDiskBlockSize, (await optimizedBytes.ReadData()).Length);
        }
    }
}