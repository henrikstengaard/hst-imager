namespace Hst.Imager.Core.Tests;

using System;
using System.IO;
using Xunit;

public class GivenCachedBlockStream
{
    private const int BlockSize = 512;
    
    [Fact]
    public void WritingBlocksThenBlocksAreCachedUntilStreamIsFlushed()
    {
        var memoryStream = new MemoryStream();
        var monitorStream = new MonitorStream(memoryStream);
        var cachedBlockStream = new CachedBlockStream(monitorStream, BlockSize);

        var blockBytes = new byte[BlockSize];
        
        Array.Fill<byte>(blockBytes, 1);
        cachedBlockStream.Seek(0, SeekOrigin.Begin);
        cachedBlockStream.Write(blockBytes, 0, blockBytes.Length);

        Array.Fill<byte>(blockBytes, 2);
        cachedBlockStream.Seek(1024, SeekOrigin.Begin);
        cachedBlockStream.Write(blockBytes, 0, blockBytes.Length);
        
        // assert - memory stream is empty
        Assert.Equal(0, memoryStream.Length);

        // assert - cached block stream has not read or written any blocks
        Assert.Empty(monitorStream.Reads);
        Assert.Empty(monitorStream.Writes);

        // act - flush blocks with changes
        cachedBlockStream.Flush();
        
        // assert - memory stream is equal to size of 3 blocks
        Assert.Equal(1536, memoryStream.Length);

        // assert - cached block stream has not read any blocks
        Assert.Empty(monitorStream.Reads);
        
        // assert - cached block stream has written offsets 0 and 1024
        Assert.Equal(2, monitorStream.Writes.Count);
        Assert.Equal(0, monitorStream.Writes[0]);
        Assert.Equal(1024, monitorStream.Writes[1]);
        
        // block 0
        var expectedBlockBytes = new byte[BlockSize];
        Array.Fill<byte>(expectedBlockBytes, 1);
        memoryStream.Seek(0, SeekOrigin.Begin);
        var bytesRead = memoryStream.Read(blockBytes, 0, blockBytes.Length);
        Assert.Equal(512, bytesRead);
        Assert.Equal(expectedBlockBytes, blockBytes);
        
        // block 1
        Array.Fill<byte>(expectedBlockBytes, 0);
        memoryStream.Seek(512, SeekOrigin.Begin);
        bytesRead = memoryStream.Read(blockBytes, 0, blockBytes.Length);
        Assert.Equal(512, bytesRead);
        Assert.Equal(expectedBlockBytes, blockBytes);
        
        // block 2
        Array.Fill<byte>(expectedBlockBytes, 2);
        memoryStream.Seek(1024, SeekOrigin.Begin);
        bytesRead = memoryStream.Read(blockBytes, 0, blockBytes.Length);
        Assert.Equal(512, bytesRead);
        Assert.Equal(expectedBlockBytes, blockBytes);
    }

    [Fact]
    public void WhenReadingSameBlockMultipleTimesThenBlockIsReadOnceAndCached()
    {
        var memoryStream = new MemoryStream(new byte[2048]);
        var monitorStream = new MonitorStream(memoryStream);
        var cachedBlockStream = new CachedBlockStream(monitorStream, BlockSize);

        var blockBytes = new byte[BlockSize];

        var expectedBlockBytes = new byte[BlockSize];
        cachedBlockStream.Seek(0, SeekOrigin.Begin);
        var bytesRead = cachedBlockStream.Read(blockBytes, 0, blockBytes.Length);
        Assert.Equal(512, bytesRead);
        Assert.Equal(expectedBlockBytes, blockBytes);
        
        // assert - offset 0 is read once
        Assert.Single(monitorStream.Reads);
        Assert.Equal(0, monitorStream.Reads[0]);
        
        cachedBlockStream.Seek(0, SeekOrigin.Begin);
        bytesRead = cachedBlockStream.Read(blockBytes, 0, blockBytes.Length);
        Assert.Equal(512, bytesRead);
        Assert.Equal(expectedBlockBytes, blockBytes);
        
        // assert - offset 0 is read once
        Assert.Single(monitorStream.Reads);
        Assert.Equal(0, monitorStream.Reads[0]);
    }
}