namespace Hst.Imager.Core;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class CachedBlockStream : Stream
{
    private readonly Stream baseStream;
    private readonly int blockSize;
    private readonly IDictionary<long, CachedBlock> blocks;
    private readonly IList<CachedBlock> changedBlocks;
    private long length;
    private long position;

    public readonly ReadOnlyDictionary<long, CachedBlock> Blocks;

    public CachedBlockStream(Stream baseStream, int blockSize = 512)
    {
        if (blockSize % 512 != 0)
        {
            throw new ArgumentException("Block size must be dividable by 512", nameof(blockSize));
        }
            
        this.baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        this.blockSize = blockSize;
        this.blocks = new Dictionary<long, CachedBlock>(1000);
        this.changedBlocks = new List<CachedBlock>(1000);
        this.Blocks = new ReadOnlyDictionary<long, CachedBlock>(this.blocks);
        this.length = this.baseStream.Length;
        this.position = 0;
    }

    public override void Flush()
    {
        FlushBlocks().GetAwaiter().GetResult();
        this.baseStream.Flush();
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await FlushBlocks();
        await this.baseStream.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = 0;
        for (var i = offset; i < Math.Min(buffer.Length, offset + count); i += blockSize)
        {
            var block = blocks.ContainsKey(position) ? blocks[position] : null;
            
            // read block bytes, if block doesn't exist and increase bytes read with block size
            if (block == null)
            {
                var blockBytes = new byte[blockSize];
                baseStream.Seek(position, SeekOrigin.Begin);
                var baseStreamBytesRead = this.baseStream.Read(blockBytes, 0, blockSize);

                if (baseStreamBytesRead != blockSize)
                {
                    throw new IOException(
                        $"Failed to read block size {this.blockSize}, bytes read {baseStreamBytesRead}");
                }

                block = new CachedBlock
                {
                    Offset = position,
                    Data = blockBytes
                };
                blocks[position] = block;
            }
            
            var bytesToCopy = i + blockSize < buffer.Length ? blockSize : buffer.Length - i;
            
            Array.Copy(block.Data, 0, buffer, i, bytesToCopy);
            position += blockSize;

            bytesRead += bytesToCopy;
        }        
        
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        position = origin switch
        {
            SeekOrigin.End => throw new NotSupportedException("Cached block memory stream doesn't support seek end"),
            SeekOrigin.Begin => 0,
            _ => position
        };

        position += offset;

        return position;
    }

    public override void SetLength(long value)
    {
        length = value;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        for (var i = offset; i < Math.Min(buffer.Length, offset + count); i += blockSize)
        {
            var blockBytes = new byte[blockSize];

            var bytesToWrite = i + blockSize < buffer.Length  ? blockSize : buffer.Length - i;
            
            Array.Copy(buffer, i, blockBytes, 0, bytesToWrite);

            var cachedBlock = new CachedBlock
            {
                Offset = position,
                Data = blockBytes
            };

            blocks[position] = cachedBlock;
            this.changedBlocks.Add(cachedBlock);
            
            position += blockSize;
        }
        
        if (position > length)
        {
            length = position;
        }
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;
    public override long Length => length;

    public override long Position
    {
        get => position;
        set => position = value;
    }

    private async Task FlushBlocks()
    {
        if (baseStream.Length != length)
        {
            baseStream.SetLength(length);
        }
        
        foreach (var block in changedBlocks)
        {
            baseStream.Seek(block.Offset, SeekOrigin.Begin);
            await baseStream.WriteAsync(block.Data, 0, block.Data.Length);
        }
        
        blocks.Clear();
        changedBlocks.Clear();
    }
}