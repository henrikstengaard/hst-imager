using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Extensions;
using Xunit;

namespace Hst.Imager.Core.Tests.StreamCopierTests;

public class GivenStreamCopierWithPhysicalDriveTestStream
{
    [Fact]
    public async Task WhenCopyFromStreamNotMatchingStreamCopierBufferSizeThenCopiedDataIsIdentical()
    {
        // arrange - data to copy
        var srcData = TestDataHelper.CreateTestData(10.MB().ToSectorSize());
        
        // arrange - src stream
        await using var srcStream = new SectorStream(new PhysicalDriveTestStream(new MemoryStream(srcData)));
        
        // arrange - dest stream
        using var destStream = new MemoryStream();

        // act - copy stream 
        var tokenSource = new CancellationTokenSource();
        var streamCopier = new StreamCopier();
        await streamCopier.Copy(tokenSource.Token, srcStream, destStream, srcData.Length);

        // assert
        var destData = destStream.ToArray();
        Assert.Equal(srcData.Length, destData.Length);
        Assert.Equal(srcData, destData);
    }

    [Fact]
    public async Task WhenCopyFromCompressedStreamToPhysicalDriveWithReadFillBufferThenWriteMatchesBufferSize()
    {
        var path = $"{Guid.NewGuid()}.img.gz";
        var size = 1024 * 1024 * 10;
        var writeCounts = new List<int>();

        try
        {
            // arrange - gzip compressed img media
            File.Copy(Path.Combine("TestData", "compressed-images", "1gb.img.gz"), path);

            // arrange - source interceptor stream overriding read with fill method
            var gZipStream = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
            await using var srcStream = new InterceptorStream(gZipStream,
                readHandler: (buffer, offset, count) => gZipStream.Fill(buffer, offset, count));

            // arrange - destination interceptor stream overriding write method to get count
            await using var destStream = new InterceptorStream(
                new PhysicalDriveTestStream(new MemoryStream(new byte[size])),
                writeHandler: (_, _, count) => writeCounts.Add(count));

            // act - copy stream
            var tokenSource = new CancellationTokenSource();
            var streamCopier = new StreamCopier();
            await streamCopier.Copy(tokenSource.Token, srcStream, destStream, size);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        // assert - stream copier wrote 10 times and all write counts are 1024 * 1024
        Assert.Equal(10, writeCounts.Count);
        foreach (var writeCount in writeCounts)
        {
            Assert.Equal(1024 * 1024, writeCount);
        }
    }

    [Fact]
    public async Task WhenCopyFromCompressedStreamToPhysicalDriveThenWriteDoesNotMatchBufferSize()
    {
        var path = $"{Guid.NewGuid()}.img.gz";
        var size = 1024 * 1024 * 10;
        var writeCounts = new List<int>();

        try
        {
            // arrange - gzip compressed img media
            File.Copy(Path.Combine("TestData", "compressed-images", "1gb.img.gz"), path);

            // arrange - source gzip stream
            await using var srcStream = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);

            // arrange - destination interceptor stream overriding write method to get count
            await using var destStream = new InterceptorStream(
                new PhysicalDriveTestStream(new MemoryStream(new byte[size])),
                writeHandler: (_, _, count) => writeCounts.Add(count));

            // arrange - stream copier
            var tokenSource = new CancellationTokenSource();
            var streamCopier = new StreamCopier();
            
            // act & assert - copy stream throws io exception
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await streamCopier.Copy(tokenSource.Token, srcStream, destStream, size));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        // assert - all write counts doesn't match 1024 * 1024
        Assert.Contains(writeCounts, x => x != 1024 * 1024);
    }
}