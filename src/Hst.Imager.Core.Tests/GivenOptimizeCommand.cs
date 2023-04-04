namespace Hst.Imager.Core.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Amiga.RigidDiskBlocks;
    using Commands;
    using Models;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class GivenOptimizeCommand : CommandTestBase
    {
        [Fact]
        public async Task WhenOptimizeImgWithSizeSetThenSizeIsChanged()
        {
            // arrange
            var path = $"{Guid.NewGuid()}.img";
            File.Copy(Path.Combine("TestData", "rigid-disk-block.img"), path, true);
            var size = 8192;
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // optimize
            var optimizeCommand = new OptimizeCommand(new NullLogger<OptimizeCommand>(), testCommandHelper, path,
                new Models.Size(size, Unit.Bytes), PartitionTable.None);
            var result = await optimizeCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert media contains optimized rigid disk block size
            var optimizedBytes = await testCommandHelper.ReadMediaData(path);
            Assert.Equal(size, optimizedBytes.Length);
        }
        
        [Fact]
        public async Task WhenOptimizeImgWithoutSizeSetThenResultIsFaulted()
        {
            // arrange
            var path = $"{Guid.NewGuid()}.img";
            var testCommandHelper = new TestCommandHelper();
            testCommandHelper.AddTestMedia(path, ImageSize);
            var cancellationTokenSource = new CancellationTokenSource();

            // optimize
            var optimizeCommand = new OptimizeCommand(new NullLogger<OptimizeCommand>(), testCommandHelper, path,
                new Models.Size(0, Unit.Bytes), PartitionTable.None);
            var result = await optimizeCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsFaulted);
        }

        [Fact]
        public async Task WhenOptimizeImgWithRdbTrueSetThenSizeIsChangedToRdbSize()
        {
            // arrange
            var path = $"{Guid.NewGuid()}.img";
            File.Copy(Path.Combine("TestData", "rigid-disk-block.img"), path, true);
            var rigidDiskBlockSize = 8192;
            var testCommandHelper = new TestCommandHelper(rigidDiskBlock: new RigidDiskBlock
            {
                DiskSize = rigidDiskBlockSize
            });
            var cancellationTokenSource = new CancellationTokenSource();

            // optimize
            var optimizeCommand = new OptimizeCommand(new NullLogger<OptimizeCommand>(), testCommandHelper, path,
                new Models.Size(rigidDiskBlockSize, Unit.Bytes), PartitionTable.Rdb);
            var result = await optimizeCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert media contains optimized rigid disk block size
            var optimizedBytes = await testCommandHelper.ReadMediaData(path);
            Assert.Equal(rigidDiskBlockSize, optimizedBytes.Length);
        }
    }
}