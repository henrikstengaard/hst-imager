namespace Hst.Imager.Core.Tests.StreamCopierTests;

using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Xunit;

public class GivenStreamCopier
{
    const int BufferSize = 512;
    
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenCopyAndSizeIsEqualToSourceThenCopiedBytesAreEqual(bool skipZeroFilled)
    {
        // arrange - source bytes
        var sourceBytes = new byte[1024 * 1024];
        for (var i = 0; i < sourceBytes.Length; i++)
        {
            sourceBytes[i] = 1;
        }
        
        // arrange - source and destination
        var source = new MemoryStream(sourceBytes);
        var destination = new MemoryStream();
        destination.SetLength(sourceBytes.Length);

        // arrange - stream copier
        var streamCopier = new StreamCopier(BufferSize);
        var cancellationTokenSource = new CancellationTokenSource();
        
        // act - copy from source to destination
        await streamCopier.Copy(cancellationTokenSource.Token, source, destination, sourceBytes.Length, 0, 0, skipZeroFilled);

        // assert - source bytes are equal to destination bytes
        var destinationBytes = destination.ToArray();
        Assert.Equal(sourceBytes.Length, destinationBytes.Length);
        Assert.Equal(sourceBytes, destinationBytes);
    }
    
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenCopyAndSizeIsSmallerThanSourceThenCopiedBytesAreEqual(bool skipZeroFilled)
    {
        // arrange - source bytes
        var sourceBytes = new byte[1024 * 1024];
        for (var i = 0; i < sourceBytes.Length; i++)
        {
            sourceBytes[i] = 1;
        }

        // arrange - size to copy
        const int size = 1000;
        
        // arrange - source and destination
        var source = new MemoryStream(sourceBytes);
        var destination = new MemoryStream();
        destination.SetLength(size);

        // arrange - stream copier
        var streamCopier = new StreamCopier(BufferSize);
        var cancellationTokenSource = new CancellationTokenSource();
        
        // act - copy from source to destination
        await streamCopier.Copy(cancellationTokenSource.Token, source, destination, size, 0, 0, skipZeroFilled);

        // assert - first 1000 bytes from source are equal to destination bytes
        var expectedDestinationBytes = sourceBytes.Take(size).ToArray();
        var destinationBytes = destination.ToArray();
        Assert.Equal(expectedDestinationBytes.Length, destinationBytes.Length);
        Assert.Equal(expectedDestinationBytes, destinationBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenCopyAndSizeIsLargerThanSourceThenCopiedBytesAreEqual(bool skipZeroFilled)
    {
        // arrange - source bytes
        var sourceBytes = new byte[1024 * 1024];
        for (var i = 0; i < sourceBytes.Length; i++)
        {
            sourceBytes[i] = 1;
        }

        // arrange - size to copy
        var size = sourceBytes.Length + 1000;
        
        // arrange - source and destination
        var source = new MemoryStream(sourceBytes);
        var destination = new MemoryStream();
        destination.SetLength(sourceBytes.Length);

        // arrange - stream copier
        var streamCopier = new StreamCopier(BufferSize);
        var cancellationTokenSource = new CancellationTokenSource();
        
        // act - copy from source to destination
        await streamCopier.Copy(cancellationTokenSource.Token, source, destination, size, 0, 0, skipZeroFilled);

        // assert - source bytes are equal to destination bytes
        var destinationBytes = destination.ToArray();
        Assert.Equal(sourceBytes.Length, destinationBytes.Length);
        Assert.Equal(sourceBytes, destinationBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenCopyAndSourceOffsetIsLargerThanZeroSourceThenCopiedBytesAreEqual(bool skipZeroFilled)
    {
        // arrange - source bytes
        var sourceBytes = new byte[1024 * 1024];
        const int bytesShiftAtOffset = 512 * 1024;
        for (var i = bytesShiftAtOffset; i < sourceBytes.Length; i++)
        {
            sourceBytes[i] = 1;
        }

        // arrange - size to copy
        var size = bytesShiftAtOffset + 1;

        // arrange - source offset to start copying from
        var sourceOffset = bytesShiftAtOffset - 1;
        
        // arrange - source and destination
        var source = new MemoryStream(sourceBytes);
        var destination = new MemoryStream();
        destination.SetLength(size);

        // arrange - stream copier
        var streamCopier = new StreamCopier(BufferSize);
        var cancellationTokenSource = new CancellationTokenSource();
        
        // act - copy from source to destination
        await streamCopier.Copy(cancellationTokenSource.Token, source, destination, size, sourceOffset, 0, skipZeroFilled);

        // assert - source bytes are equal to destination bytes
        var destinationBytes = destination.ToArray();
        var expectedDestinationBytes = new byte[] { 0 }.Concat(sourceBytes.Skip(bytesShiftAtOffset)).ToArray();
        Assert.Equal(expectedDestinationBytes.Length, destinationBytes.Length);
        Assert.Equal(expectedDestinationBytes, destinationBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WhenCopyAndDestinationOffsetIsLargerThanZeroSourceThenCopiedBytesAreEqual(bool skipZeroFilled)
    {
        // arrange - source bytes
        var sourceBytes = new byte[1024 * 1024];
        const int bytesShiftAtOffset = 512 * 1024;
        for (var i = bytesShiftAtOffset; i < sourceBytes.Length; i++)
        {
            sourceBytes[i] = 1;
        }

        // arrange - size to copy
        var size = bytesShiftAtOffset + 1;

        // arrange - source offset to copy from
        var sourceOffset = bytesShiftAtOffset - 1;

        // arrange - destination offset to copy to
        var destinationOffset = 1024;
        
        // arrange - source and destination
        var source = new MemoryStream(sourceBytes);
        var destination = new MemoryStream();
        destination.SetLength(size);
        
        // arrange - write first 1024 bytes to destination
        await destination.WriteBytes(new byte[1024]);

        // arrange - stream copier
        var streamCopier = new StreamCopier(BufferSize);
        var cancellationTokenSource = new CancellationTokenSource();
        
        // act - copy from source to destination
        await streamCopier.Copy(cancellationTokenSource.Token, source, destination, size, sourceOffset, destinationOffset, skipZeroFilled);

        // assert - destination bytes contain first 1024 zero bytes and data bytes copied from source
        var destinationBytes = destination.ToArray();
        var expectedDestinationBytes =
            new byte[1024].Concat(sourceBytes.Skip(sourceOffset).Take(size)).ToArray();
        Assert.Equal(expectedDestinationBytes.Length, destinationBytes.Length);
        Assert.Equal(expectedDestinationBytes, destinationBytes);
    }

    public byte[] CreateTestData(long size)
    {
        var data = new byte[size];

        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }

        return data;
    }
    
    [Fact]
    public async Task WhenCopy1MbFrom10MbSrcToDestWith1MbBufferThenDataIsEqual()
    {
        // arrange - test data
        var data = CreateTestData(10.MB());
        
        // arrange - source stream with test data
        using var source = new MemoryStream();
        await source.WriteBytes(data);

        // arrange - destination stream
        using var destination = new MemoryStream();
        
        // arrange - stream copier
        var streamCopier = new StreamCopier(1024 * 1024);
        
        // act - copy from source to destination
        var size = (int)1.MB();
        var sourceOffset = 0;
        var destinationOffset = 0;
        var cancellationTokenSource = new CancellationTokenSource();
        await streamCopier.Copy(cancellationTokenSource.Token, source, destination, size, sourceOffset, destinationOffset, true);

        // get source bytes
        var sourceBytes = data.Take(size).ToArray();
        
        // get destination bytes
        var destinationBytes = destination.ToArray();
        
        // assert - source bytes are equal to destination bytes
        Assert.Equal(sourceBytes.Length, destinationBytes.Length);
        Assert.Equal(sourceBytes, destinationBytes);
    }
}