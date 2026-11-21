using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Optimization;
using Xunit;

namespace Hst.Imager.Core.Tests.OptimizationTests;

public class GivenImageCompactor
{
    private const int Size = 4 * 1024 * 1024;

    [Fact]
    public async Task When_CompactingWithoutFreeRanges_Then_DataIsEqualAndZeroFilledDataIsSkipped()
    {
        // arrange - source with data in first and third 64kb chunk, rest zero filled
        var sourceBytes = new byte[Size];
        Array.Fill(sourceBytes, (byte)1, 0, ImageCompactor.ZeroCheckSize);
        Array.Fill(sourceBytes, (byte)2, ImageCompactor.ZeroCheckSize * 2 + 100, 10);
        var source = new MemoryStream(sourceBytes);
        var destination = CreateDestination();

        // act - compact source to destination
        var result = await new ImageCompactor().Compact(source, destination, Size, new AllocationMap(), false,
            CancellationToken.None);

        // assert - result is success and only two 64kb chunks are written
        Assert.True(result.IsSuccess);
        Assert.Equal(ImageCompactor.ZeroCheckSize * 2, result.Value.BytesWritten);
        Assert.Equal(0, result.Value.BytesFree);
        Assert.Equal(Size - ImageCompactor.ZeroCheckSize * 2, result.Value.BytesZeroFilled);

        // assert - destination is equal to source
        Assert.Equal(sourceBytes, destination.ToArray());
    }

    [Fact]
    public async Task When_CompactingWithFreeRanges_Then_FreeRangesAreNotCopied()
    {
        // arrange - source filled with data
        var sourceBytes = new byte[Size];
        Array.Fill(sourceBytes, (byte)0xaa);
        var source = new MemoryStream(sourceBytes);
        var destination = CreateDestination();

        // arrange - allocation map with 1mb free range starting at 1mb and 512 bytes free at 3mb
        var allocationMap = new AllocationMap();
        allocationMap.AddFree(1024 * 1024, 1024 * 1024);
        allocationMap.AddFree(3 * 1024 * 1024, 512);

        // act - compact source to destination
        var result = await new ImageCompactor().Compact(source, destination, Size, allocationMap, false,
            CancellationToken.None);

        // assert - result is success and free ranges are skipped
        Assert.True(result.IsSuccess);
        Assert.Equal(1024 * 1024 + 512, result.Value.BytesFree);
        Assert.Equal(Size - (1024 * 1024 + 512), result.Value.BytesWritten);

        // assert - destination has zeros in free ranges and data elsewhere
        var expectedBytes = new byte[Size];
        Array.Fill(expectedBytes, (byte)0xaa);
        Array.Clear(expectedBytes, 1024 * 1024, 1024 * 1024);
        Array.Clear(expectedBytes, 3 * 1024 * 1024, 512);
        Assert.Equal(expectedBytes, destination.ToArray());
    }

    [Fact]
    public async Task When_CompactingWithByteSwap_Then_DataIsByteSwapped()
    {
        // arrange - source with data
        var sourceBytes = new byte[Size];
        for (var i = 0; i < 1024; i++)
        {
            sourceBytes[i] = (byte)i;
        }

        var source = new MemoryStream(sourceBytes);
        var destination = CreateDestination();

        // act - compact source to destination with byte swap
        var result = await new ImageCompactor().Compact(source, destination, Size, new AllocationMap(), true,
            CancellationToken.None);

        // assert - result is success and data is byte swapped
        Assert.True(result.IsSuccess);
        var destinationBytes = destination.ToArray();
        for (var i = 0; i < 1024; i += 2)
        {
            Assert.Equal(sourceBytes[i + 1], destinationBytes[i]);
            Assert.Equal(sourceBytes[i], destinationBytes[i + 1]);
        }
    }

    [Fact]
    public async Task When_CompactingIsCancelled_Then_ResultIsFaulted()
    {
        // arrange - source, destination and cancelled token
        var source = new MemoryStream(new byte[Size]);
        var destination = CreateDestination();
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // act - compact source to destination
        var result = await new ImageCompactor().Compact(source, destination, Size, new AllocationMap(), false,
            cancellationTokenSource.Token);

        // assert - result is faulted
        Assert.True(result.IsFaulted);
    }

    private static MemoryStream CreateDestination()
    {
        var stream = new MemoryStream();
        stream.SetLength(Size);
        return stream;
    }
}
