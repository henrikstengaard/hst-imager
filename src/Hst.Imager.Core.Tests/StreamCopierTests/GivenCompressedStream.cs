using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Hst.Imager.Core.Extensions;
using Xunit;

namespace Hst.Imager.Core.Tests.StreamCopierTests;

public class GivenCompressedStream
{
    [Fact]
    public async Task WhenReadThenBytesReadMatchesBufferSize()
    {
        var path = $"{Guid.NewGuid()}.img.gz";

        try
        {
            // arrange - gzip compressed img media
            File.Copy(Path.Combine("TestData", "compressed-images", "1gb.img.gz"), path);

            // arrange - gzip stream
            await using var stream = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);

            // act - read from stream
            var buffer = new byte[1024 * 1024];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            
            // assert - bytes read from gz compressed stream is not equal to buffer length
            Assert.NotEqual(1024 * 1024, bytesRead);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
    
    [Fact]
    public async Task WhenReadWithInterceptorStreamFillingBufferThenBytesReadMatchesBufferSize()
    {
        var path = $"{Guid.NewGuid()}.img.gz";

        try
        {
            // arrange - gzip compressed img media
            File.Copy(Path.Combine("TestData", "compressed-images", "1gb.img.gz"), path);

            // arrange - interceptor stream overriding read with fill method
            var gZipStream = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
            await using var stream = new InterceptorStream(gZipStream,
                readHandler: (buffer, offset, count) => gZipStream.Fill(buffer, offset, count));

            // act - read from stream
            var buffer = new byte[1024 * 1024];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            
            // assert - buffer length was read
            Assert.Equal(1024 * 1024, bytesRead);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}