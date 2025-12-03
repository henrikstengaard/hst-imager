using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hst.Imager.Core;

public class LayeredStream : Stream
{
    private class LayerCacheItem
    {
        public long BlockNumber { get; set; }
        public long BlockOffset { get; set; }
        public int Size { get; set; }
        public bool IsChanged { get; set; }
    }
    
    private const string Magic = "HILY";
    private const int HeaderSize = 4 + 8 + 4; // magic (4 bytes) + size (8 bytes) + block size (8 bytes)

    private readonly Stream baseStream;
    private readonly string layerPath;
    private readonly Stream layeredStream;
    private readonly long size;
    private readonly int blockSize;
    private readonly int numberOfBlocks;
    private readonly IDictionary<long, LayerCacheItem> blockAllocationTable;
    private readonly byte[] blockBuffer;
    
    private long position;
    private long nextBlockOffset;

    public bool IsDisposed { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="baseStream"></param>
    /// <param name="layeredStream"></param>
    /// <param name="size">Size of layered stream.</param>
    /// <param name="blockSize">Size of blocks the layered stream stores.</param>
    public LayeredStream(Stream baseStream, Stream layeredStream, long size,
        int blockSize = 1024 * 1024) : this(baseStream, string.Empty, layeredStream, size, blockSize)
    {
    }    

    /// <summary>
    /// 
    /// </summary>
    /// <param name="baseStream"></param>
    /// <param name="layerPath"></param>
    /// <param name="layeredStream"></param>
    /// <param name="size">Size of layered stream.</param>
    /// <param name="blockSize">Size of blocks the layered stream stores.</param>
    public LayeredStream(Stream baseStream, string layerPath, Stream layeredStream, long size, int blockSize = 1024 * 1024)
    {
        this.baseStream = baseStream;
        this.layerPath = layerPath;
        this.layeredStream = layeredStream;
        this.size = size;
        this.blockSize = blockSize;
        blockBuffer = new byte[blockSize];
        numberOfBlocks = Convert.ToInt32(Math.Ceiling((double)size / blockSize));
        var blockAllocationTableSize = numberOfBlocks * 8;
        blockAllocationTable = new Dictionary<long, LayerCacheItem>();
        nextBlockOffset = HeaderSize + blockAllocationTableSize;
    }
    /*
a stream that creates a layer of updates on top of an underlying stream.
it supports reading from the underlying stream, and writing to the layer.
the layer is stored in memory for simplicity, but could be stored in a file or other storage.

    

     */

    protected override void Dispose(bool disposing)
    {
        if (IsDisposed)
        {
            return;
        }
        
        if (disposing)
        {
            FlushLayer().GetAwaiter().GetResult();
            
            layeredStream.Close();
            layeredStream.Dispose();
            
            baseStream.Close();
            baseStream.Dispose();
            
            if (!string.IsNullOrEmpty(layerPath) && File.Exists(layerPath))
            {
                File.Delete(layerPath);
            }
        }
        base.Dispose(disposing);
        
        IsDisposed = true;
    }

    public void Initialize()
    {
        if (layeredStream.Length == 0)
        {
            layeredStream.Write(Encoding.ASCII.GetBytes(Magic));
            layeredStream.Write(BitConverter.GetBytes(size));
            layeredStream.Write(BitConverter.GetBytes(blockSize));
        
            // initialize block allocation table with zeros
            for (var i = 0; i < numberOfBlocks; i++)
            {
                layeredStream.Write(BitConverter.GetBytes(0L));
            }

            return;
        }

        // read and validate header
        var headerBytes = new byte[HeaderSize];
        layeredStream.Seek(0, SeekOrigin.Begin);
        layeredStream.ReadExactly(headerBytes, 0, HeaderSize);
        var magic = Encoding.ASCII.GetString(headerBytes, 0, 4);
        if (magic != Magic)
        {
            throw new IOException("Invalid layered stream magic");
        }

        // read and validate size
        var sizeBytes = new byte[8];
        layeredStream.ReadExactly(sizeBytes, 0, 8);
        if (BitConverter.ToInt64(sizeBytes, 0) != size)
        {
            throw new IOException("Invalid layered stream size");
        }
        
        // read and validate block size
        var blockSizeBytes = new byte[8];
        layeredStream.ReadExactly(blockSizeBytes, 0, 8);
        if (BitConverter.ToInt64(blockSizeBytes, 0) != blockSize)
        {
            throw new IOException("Invalid layered stream block size");
        }
        
        // read block allocation table
        for (var blockNumber = 0; blockNumber < numberOfBlocks; blockNumber++)
        {
            var blockOffsetBytes = new byte[8];
            layeredStream.ReadExactly(blockOffsetBytes, 0, 8);
            var blockOffset = BitConverter.ToInt64(blockOffsetBytes, 0);
            if (blockOffset == 0)
            {
                continue;
            }
            var cacheLayerItem = new LayerCacheItem
            {
                BlockNumber = blockNumber,
                BlockOffset = blockOffset,
                Size = 0,
                IsChanged = false
            };
            blockAllocationTable.Add(blockNumber, cacheLayerItem);
            nextBlockOffset = blockOffset + 8 + 4 + blockSize;
        }
    }

    public override void Flush()
    {
        layeredStream.Flush();
    }

    public async Task FlushLayer(CancellationToken cancellationToken = default)
    {
        foreach (var layerCacheItem in blockAllocationTable.Values)
        {
            if (!layerCacheItem.IsChanged)
            {
                continue;
            }
            
            layeredStream.Position = layerCacheItem.BlockOffset;
            
            // read and validate block number
            var blockOffsetBytes = new byte[8];
            await layeredStream.ReadExactlyAsync(blockOffsetBytes, 0, 8, cancellationToken);
            var blockOffset = BitConverter.ToInt64(blockOffsetBytes, 0);
            
            // validate block number
            if (blockOffset != layerCacheItem.BlockNumber)
            {
                throw new IOException("Block number mismatch during flush");
            }

            // read block size
            var layeredBlockSizeBytes = new byte[4];
            await layeredStream.ReadExactlyAsync(layeredBlockSizeBytes, 0, 4, cancellationToken);
            var layeredBlockSize = BitConverter.ToInt32(layeredBlockSizeBytes, 0);
            
            // read block data from layered stream
            await layeredStream.ReadExactlyAsync(blockBuffer, 0, layeredBlockSize, cancellationToken);
            
            // write block data to base stream
            baseStream.Position = layerCacheItem.BlockNumber * blockSize;
            await baseStream.WriteAsync(blockBuffer.AsMemory(0, layeredBlockSize), cancellationToken);

            layerCacheItem.IsChanged = false;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var blockNumber = position / blockSize;
        var bufferPosition = 0;

        var bytesRead = 0;
        do
        {
            if (!blockAllocationTable.ContainsKey(blockNumber))
            {
                ReadBlockToLayer(blockNumber);
            }
            
            var layerCacheItem = blockAllocationTable[blockNumber];
            var positionInBlock = Convert.ToInt32(position % blockSize);
            var bytesToRead = Math.Min(count - bufferPosition, blockSize - positionInBlock);
            layeredStream.Position = layerCacheItem.BlockOffset + 8 + 4 + positionInBlock;
            bytesRead += layeredStream.Read(buffer, offset + bufferPosition, bytesToRead);
            position += bytesRead;
            bufferPosition += bytesRead;
            blockNumber++;
        } while (bufferPosition < count && bytesRead > 0);

        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        switch (origin)
        {
            case SeekOrigin.Begin:
                position = offset;
                break;
            case SeekOrigin.Current:
                position += offset;
                break;
            case SeekOrigin.End:
                position = size + offset;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(origin), origin, null);
        }

        return position;
    }

    public override void SetLength(long value)
    {
    }

    private void ReadBlockToLayer(long blockNumber)
    {
        baseStream.Position = blockNumber * blockSize;
        var bytesRead = baseStream.Read(blockBuffer, 0, blockSize);
        
        layeredStream.Position = nextBlockOffset;
        layeredStream.Write(BitConverter.GetBytes(blockNumber));
        layeredStream.Write(BitConverter.GetBytes(bytesRead));
        layeredStream.Write(blockBuffer, 0, bytesRead);
        
        // update block allocation table
        var layerCacheItem = new LayerCacheItem
        {
            BlockNumber = blockNumber,
            BlockOffset = nextBlockOffset,
            Size = bytesRead,
            IsChanged = false
        };
        blockAllocationTable[blockNumber] = layerCacheItem;
        
        // update next block offset
        nextBlockOffset += 8 + 4 + blockSize;
        
        // update block allocation table in layered stream
        layeredStream.Seek(HeaderSize + blockNumber * 8, SeekOrigin.Begin);
        layeredStream.Write(BitConverter.GetBytes(layerCacheItem.BlockOffset));
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var blockNumber = position / blockSize;
        var bufferPosition = 0;
        
        do
        {
            if (!blockAllocationTable.ContainsKey(blockNumber))
            {
                ReadBlockToLayer(blockNumber);
            }

            var layerCacheItem = blockAllocationTable[blockNumber];
            var positionInBlock = Convert.ToInt32(position % blockSize);
            var bytesToWrite = Math.Min(count - bufferPosition, blockSize - positionInBlock);
            layeredStream.Position = layerCacheItem.BlockOffset + 8 + 4 + positionInBlock;
            layeredStream.Write(buffer, offset + bufferPosition, bytesToWrite);
            layerCacheItem.IsChanged = true;
            position += bytesToWrite;
            bufferPosition += bytesToWrite;
            blockNumber++;
        } while (bufferPosition < count);
    }

    public override bool CanRead => baseStream.CanRead;
    public override bool CanSeek => baseStream.CanSeek;
    public override bool CanWrite => baseStream.CanWrite;
    public override long Length => baseStream.Length;

    public override long Position
    {
        get => position;
        set => position = value;
    }
}