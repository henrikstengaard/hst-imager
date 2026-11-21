using System;
using System.IO;
using System.Threading.Tasks;
using Hst.Imager.Core.SparseFiles;
using Xunit;

namespace Hst.Imager.Core.Tests;

public class GivenSparseFile
{
    private const long OneMegabyte = 1024 * 1024;

    [Fact]
    public async Task When_GettingSparseFileSizeOfRegularFile_Then_SizeIsInBytes()
    {
        // arrange - regular file with 1MB random data
        var path = $"{Guid.NewGuid()}.img";
        var data = new byte[OneMegabyte];
        new Random(1).NextBytes(data);
        await File.WriteAllBytesAsync(path, data);

        try
        {
            // act - get sparse file size
            var sparseFileSize = SparseFile.GetSparseFileSize(path);

            // assert - sparse file size is in bytes, at least file size and less than file size plus
            // 1MB allowing for file system block allocation. a size in 512 or 1024 byte blocks would be
            // 2048 or 1024 and fail
            Assert.InRange(sparseFileSize, OneMegabyte, 2 * OneMegabyte);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task When_GettingSizeOnDiskOfSparseFile_Then_SizeIsLessThanFileSize()
    {
        // arrange - sparse file of 20MB with 1MB random data written at start
        var path = $"{Guid.NewGuid()}.img";
        var fileSize = 20 * OneMegabyte;
        SparseFile.CreateSparseFile(path, fileSize);
        var data = new byte[OneMegabyte];
        new Random(1).NextBytes(data);
        await using (var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite))
        {
            await stream.WriteAsync(data);

            // flush to disk, as file systems with delayed allocation like refs don't report
            // clusters as allocated until cached data is written to disk
            stream.Flush(flushToDisk: true);
        }

        try
        {
            // act - get size on disk
            var sizeOnDisk = SparseFile.GetSizeOnDisk(path);

            // assert - file size is unchanged and size on disk is at least written data and less than
            // file size, as unwritten part of sparse file is not allocated
            Assert.Equal(fileSize, new FileInfo(path).Length);
            var driveFormat = new DriveInfo(Path.GetFullPath(path)).DriveFormat;
            Assert.True(sizeOnDisk >= OneMegabyte && sizeOnDisk < fileSize,
                $"Expected size on disk between {OneMegabyte} and {fileSize - 1}, but was {sizeOnDisk} on file system '{driveFormat}'");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void When_GettingSizeOnDiskOfNonExistingFile_Then_SizeIsZero()
    {
        // arrange - path to non existing file
        var path = $"{Guid.NewGuid()}.img";

        // act - get size on disk
        var sizeOnDisk = SparseFile.GetSizeOnDisk(path);

        // assert - size on disk is zero
        Assert.Equal(0, sizeOnDisk);
    }
}
