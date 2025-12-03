using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Hst.Imager.Core.Tests.LayeredStreamTests;

public class GivenLayeredStreamWithEmptyLayer
{
    private const int LongSize = 8;
    private const int IntSize = 4;
    private const int MagicSize = 4;
    private const int HeaderSize = MagicSize + LongSize + IntSize;
    private const int BlockHeaderSize = LongSize + IntSize;

    private const int BlockSize = 512;
    private const int NumberOfBlocks = 10;
    private const int Size = BlockSize * NumberOfBlocks;
    private const int BlockAllocationTableSize = NumberOfBlocks * LongSize;
    
    private readonly byte[] blockTestDataBytes = new byte[BlockSize];

    public GivenLayeredStreamWithEmptyLayer()
    {
        for (var i = 0; i < BlockSize; i++)
        {
            blockTestDataBytes[i] = (byte)(i % 256);
        }
    }
    
    [Fact]
    public void When_ReadingDataFromSingleBlock_Then_DataIsReadFromBaseToSingleBlockLayer()
    {
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(Size);

        // arrange - empty layer stream
        using var layerStream = new MemoryStream();

        // arrange - layered stream on top of base and layer streams
        using var layeredStream = new LayeredStream(baseStream, layerStream, Size, BlockSize);
        layeredStream.Initialize();

        // arrange - write data to base stream at position 0
        baseStream.Position = 0;
        baseStream.Write(this.blockTestDataBytes, 0, BlockSize);

        // act - read data at position 0 from layered stream
        var blockBytes = new byte[BlockSize];
        layeredStream.Position = 0;
        layeredStream.ReadExactly(blockBytes, 0, BlockSize);

        // assert - read data is equal to base stream data
        Assert.Equal(blockTestDataBytes.Length, blockBytes.Length);
        Assert.Equal(blockTestDataBytes, blockBytes);
        
        // assert - layered stream structure is equal to expected size
        const int numberOfBlocks = 1;
        var expectedLayeredStreamSize = HeaderSize + BlockAllocationTableSize + (LongSize + IntSize + BlockSize) * numberOfBlocks;
        Assert.Equal(expectedLayeredStreamSize, layerStream.Length);
        
        // read block allocation table
        var blockAllocationTableBytes = new byte[BlockAllocationTableSize];
        layerStream.Position = HeaderSize;
        layerStream.ReadExactly(blockAllocationTableBytes, 0, BlockAllocationTableSize);
        
        // create expected block allocation table
        var expectedBlockAllocationTableBytes = new byte[BlockAllocationTableSize];

        // write block number 0 offset in expected block allocation table position 0 (offset 0)
        var blockOffsetBytes = BitConverter.GetBytes(Convert.ToInt64(HeaderSize + BlockAllocationTableSize));
        const int blockAllocationTableOffset = 0;
        Array.Copy(blockOffsetBytes, 0, expectedBlockAllocationTableBytes, blockAllocationTableOffset, LongSize);
        
        // assert block allocation table
        Assert.Equal(expectedBlockAllocationTableBytes.Length, blockAllocationTableBytes.Length);
        Assert.Equal(expectedBlockAllocationTableBytes, blockAllocationTableBytes);

        // read block
        var block0Bytes = new byte[BlockHeaderSize + BlockSize];
        layerStream.Position = HeaderSize + BlockAllocationTableSize;
        layerStream.ReadExactly(block0Bytes, 0, block0Bytes.Length);
        
        // assert block
        var blockHeaderSize = BlockHeaderSize;
        var expectedBlockBytes = new byte[blockHeaderSize + BlockSize];
        Array.Copy(BitConverter.GetBytes(0L), 0, expectedBlockBytes, 0, LongSize); // block number
        Array.Copy(BitConverter.GetBytes(BlockSize), 0, expectedBlockBytes, 8, IntSize); // block size
        var blockBytesToCopy = BlockSize;
        Array.Copy(blockTestDataBytes, 0, expectedBlockBytes, blockHeaderSize, blockBytesToCopy); // block data
        Assert.Equal(expectedBlockBytes.Length, block0Bytes.Length);
        Assert.Equal(expectedBlockBytes, block0Bytes);
    }

    [Fact]
    public void When_ReadingDataFromMultipleBlocks_Then_DataIsReadFromBaseToMultipleBlocksInLayer()
    {
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(Size);

        // arrange - empty layer stream
        using var layerStream = new MemoryStream();

        // arrange - layered stream on top of base and layer streams
        using var layeredStream = new LayeredStream(baseStream, layerStream, Size, BlockSize);
        layeredStream.Initialize();

        // arrange - write data to base stream at position 0
        baseStream.Position = 0;
        baseStream.Write(this.blockTestDataBytes, 0, BlockSize);

        // act - read data at position 100 from layered stream
        var blockBytes = new byte[BlockSize];
        layeredStream.Position = 100;
        layeredStream.ReadExactly(blockBytes, 0, BlockSize);
        
        // assert - read data is equal to base stream data
        var expectedBlockBytes = new byte[BlockSize];
        Array.Copy(blockTestDataBytes, 100, expectedBlockBytes, 0, BlockSize - 100);
        Assert.Equal(expectedBlockBytes.Length, blockBytes.Length);
        Assert.Equal(expectedBlockBytes, blockBytes);

        // assert - layered stream structure is equal to expected size
        const int numberOfBlocks = 2;
        var expectedLayeredStreamSize = HeaderSize + BlockAllocationTableSize + (LongSize + IntSize + BlockSize) * numberOfBlocks;
        Assert.Equal(expectedLayeredStreamSize, layerStream.Length);
        
        // read block allocation table
        var blockAllocationTableBytes = new byte[BlockAllocationTableSize];
        layerStream.Position = HeaderSize;
        layerStream.ReadExactly(blockAllocationTableBytes, 0, BlockAllocationTableSize);
        
        // create expected block allocation table
        var expectedBlockAllocationTableBytes = new byte[BlockAllocationTableSize];

        // write block number 0 offset in expected block allocation table position 0
        var blockOffsetBytes = BitConverter.GetBytes(Convert.ToInt64(HeaderSize + BlockAllocationTableSize));
        var blockAllocationTableOffset = 0; // offset 0
        Array.Copy(blockOffsetBytes, 0, expectedBlockAllocationTableBytes, blockAllocationTableOffset, LongSize);

        // write block number 1 offset in expected block allocation table position 1
        blockOffsetBytes = BitConverter.GetBytes(Convert.ToInt64(HeaderSize + BlockAllocationTableSize + BlockHeaderSize + BlockSize));
        blockAllocationTableOffset = LongSize; // offset 8
        Array.Copy(blockOffsetBytes, 0, expectedBlockAllocationTableBytes, blockAllocationTableOffset, LongSize);
        
        // assert block allocation table
        Assert.Equal(expectedBlockAllocationTableBytes.Length, blockAllocationTableBytes.Length);
        Assert.Equal(expectedBlockAllocationTableBytes, blockAllocationTableBytes);

        // read blocks
        var block1Bytes = new byte[BlockHeaderSize + BlockSize];
        layerStream.Position = HeaderSize + BlockAllocationTableSize;
        layerStream.ReadExactly(block1Bytes, 0, block1Bytes.Length);
        var block2Bytes = new byte[BlockHeaderSize + BlockSize];
        layerStream.Position = HeaderSize + BlockAllocationTableSize + block1Bytes.Length;
        layerStream.ReadExactly(block2Bytes, 0, block2Bytes.Length);
        
        // assert block 0
        var blockHeaderSize = BlockHeaderSize;
        var expectedBlock1Bytes = new byte[blockHeaderSize + BlockSize];
        Array.Copy(BitConverter.GetBytes(0L), 0, expectedBlock1Bytes, 0, LongSize); // block number
        Array.Copy(BitConverter.GetBytes(BlockSize), 0, expectedBlock1Bytes, 8, IntSize); // block size
        Array.Copy(blockTestDataBytes, 0, expectedBlock1Bytes, blockHeaderSize, blockTestDataBytes.Length); // block data
        Assert.Equal(expectedBlock1Bytes.Length, block1Bytes.Length);
        Assert.Equal(expectedBlock1Bytes, block1Bytes);
        
        // assert block 1
        var expectedBlock2Bytes = new byte[BlockHeaderSize + BlockSize];
        Array.Copy(BitConverter.GetBytes(1L), 0, expectedBlock2Bytes, 0, LongSize); // block number
        Array.Copy(BitConverter.GetBytes(BlockSize), 0, expectedBlock2Bytes, 8, IntSize); // block size
        Assert.Equal(expectedBlock2Bytes.Length, block2Bytes.Length);
        Assert.Equal(expectedBlock2Bytes, block2Bytes);
    }

    [Fact]
    public void When_WritingDataToSingleBlock_Then_DataIsWrittenToBlockInLayer()
    {
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(Size);
        
        // arrange - empty layer stream
        using var layerStream = new MemoryStream();
        
        // arrange - layered stream on top of base and layer streams
        using var layeredStream = new LayeredStream(baseStream, layerStream, Size, BlockSize);
        layeredStream.Initialize();
        
        // act - write data at position 50 to layered stream
        layeredStream.Position = 50;
        layeredStream.Write(this.blockTestDataBytes, 0, 4);
        
        // assert - layered stream structure is equal to expected size
        const int numberOfBlocks = 1;
        var expectedLayeredStreamSize = HeaderSize + BlockAllocationTableSize + (LongSize + IntSize + BlockSize) * numberOfBlocks;
        Assert.Equal(expectedLayeredStreamSize, layerStream.Length);
        
        // read block allocation table
        var blockAllocationTableBytes = new byte[BlockAllocationTableSize];
        layerStream.Position = HeaderSize;
        layerStream.ReadExactly(blockAllocationTableBytes, 0, BlockAllocationTableSize);
        
        // create expected block allocation table
        var expectedBlockAllocationTableBytes = new byte[BlockAllocationTableSize];

        // write block number 0 offset in expected block allocation table position 0
        var blockOffsetBytes = BitConverter.GetBytes(Convert.ToInt64(HeaderSize + BlockAllocationTableSize));
        const int blockAllocationTableOffset = 0; // offset 0
        Array.Copy(blockOffsetBytes, 0, expectedBlockAllocationTableBytes, blockAllocationTableOffset, LongSize);
        
        // assert block allocation table
        Assert.Equal(expectedBlockAllocationTableBytes.Length, blockAllocationTableBytes.Length);
        Assert.Equal(expectedBlockAllocationTableBytes, blockAllocationTableBytes);
        
        // read block
        var block0Bytes = new byte[BlockHeaderSize + BlockSize];
        layerStream.Position = HeaderSize + BlockAllocationTableSize;
        layerStream.ReadExactly(block0Bytes, 0, block0Bytes.Length);
        
        // assert block
        var blockHeaderSize = BlockHeaderSize;
        var expectedBlockBytes = new byte[blockHeaderSize + BlockSize];
        Array.Copy(BitConverter.GetBytes(0L), 0, expectedBlockBytes, 0, LongSize); // block number
        Array.Copy(BitConverter.GetBytes(BlockSize), 0, expectedBlockBytes, 8, IntSize); // block size
        var blockBytesOffset = 50;
        var blockBytesToWrite = 4;
        Array.Copy(blockTestDataBytes, 0, expectedBlockBytes, blockHeaderSize + blockBytesOffset, blockBytesToWrite); // block data
        Assert.Equal(expectedBlockBytes.Length, block0Bytes.Length);
        Assert.Equal(expectedBlockBytes, block0Bytes);
        
        // assert base stream is unchanged
        var baseStreamBytes = new byte[Size];
        baseStream.Position = 0;
        baseStream.ReadExactly(baseStreamBytes, 0, Size);
        var expectedBaseStreamBytes = new byte[Size];
        Assert.Equal(expectedBaseStreamBytes.Length, baseStreamBytes.Length);
        Assert.Equal(expectedBaseStreamBytes, baseStreamBytes);
    }
    
    [Fact]
    public void When_WritingDataSpanningMultipleBlocks_Then_DataIsWrittenToMultipleBlocksInLayer()
    {
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(Size);
        
        // arrange - empty layer stream
        using var layerStream = new MemoryStream();
        
        // arrange - layered stream on top of base and layer streams
        using var layeredStream = new LayeredStream(baseStream, layerStream, Size, BlockSize);
        layeredStream.Initialize();
        
        // act - write data at position 600 to layered stream, which is larger than block size and causes data to span multiple blocks
        layeredStream.Position = 600;
        layeredStream.Write(blockTestDataBytes, 0, blockTestDataBytes.Length);
        
        // assert - layered stream structure is equal to expected size
        const int numberOfBlocks = 2;
        var expectedLayeredStreamSize = HeaderSize + BlockAllocationTableSize + (BlockHeaderSize + BlockSize) * numberOfBlocks;
        Assert.Equal(expectedLayeredStreamSize, layerStream.Length);
        
        // read block allocation table
        var blockAllocationTableBytes = new byte[BlockAllocationTableSize];
        layerStream.Position = HeaderSize;
        layerStream.ReadExactly(blockAllocationTableBytes, 0, BlockAllocationTableSize);
        
        // create expected block allocation table
        var expectedBlockAllocationTableBytes = new byte[BlockAllocationTableSize];

        // write block number 1 offset in expected block allocation table position 1
        var blockOffsetBytes = BitConverter.GetBytes(Convert.ToInt64(HeaderSize + BlockAllocationTableSize));
        var blockAllocationTableOffset = LongSize; // offset 8
        Array.Copy(blockOffsetBytes, 0, expectedBlockAllocationTableBytes, blockAllocationTableOffset, LongSize);

        // write block number 2 offset in expected block allocation table position 2 (offset 16)
        blockOffsetBytes = BitConverter.GetBytes(Convert.ToInt64(HeaderSize + BlockAllocationTableSize + BlockHeaderSize + BlockSize));
        blockAllocationTableOffset = 2 * LongSize; // offset 16
        Array.Copy(blockOffsetBytes, 0, expectedBlockAllocationTableBytes, blockAllocationTableOffset, LongSize);

        // assert block allocation table
        Assert.Equal(expectedBlockAllocationTableBytes.Length, blockAllocationTableBytes.Length);
        Assert.Equal(expectedBlockAllocationTableBytes, blockAllocationTableBytes);
        
        // read blocks
        var block1Bytes = new byte[BlockHeaderSize + BlockSize];
        layerStream.Position = HeaderSize + BlockAllocationTableSize;
        layerStream.ReadExactly(block1Bytes, 0, block1Bytes.Length);
        var block2Bytes = new byte[BlockHeaderSize + BlockSize];
        layerStream.Position = HeaderSize + BlockAllocationTableSize + block1Bytes.Length;
        layerStream.ReadExactly(block2Bytes, 0, block2Bytes.Length);
        
        // assert block 1
        var blockHeaderSize = BlockHeaderSize;
        var expectedBlock1Bytes = new byte[blockHeaderSize + BlockSize];
        Array.Copy(BitConverter.GetBytes(1L), 0, expectedBlock1Bytes, 0, LongSize); // block number
        Array.Copy(BitConverter.GetBytes(BlockSize), 0, expectedBlock1Bytes, 8, IntSize); // block size
        var blockBytesOffset = 600 % BlockSize;
        var block1BytesToWrite = BlockSize - blockBytesOffset;
        Array.Copy(blockTestDataBytes, 0, expectedBlock1Bytes, blockHeaderSize + blockBytesOffset, block1BytesToWrite); // block data
        Assert.Equal(expectedBlock1Bytes.Length, block1Bytes.Length);
        Assert.Equal(expectedBlock1Bytes, block1Bytes);
        
        // assert block 2
        var expectedBlock2Bytes = new byte[BlockHeaderSize + BlockSize];
        Array.Copy(BitConverter.GetBytes(2L), 0, expectedBlock2Bytes, 0, LongSize); // block number
        Array.Copy(BitConverter.GetBytes(BlockSize), 0, expectedBlock2Bytes, 8, IntSize); // block size
        blockBytesOffset = BlockSize - (600 % BlockSize);
        var block2BytesToWrite = BlockSize - blockBytesOffset;
        Array.Copy(blockTestDataBytes, blockBytesOffset, expectedBlock2Bytes, blockHeaderSize, block2BytesToWrite); // block data
        Assert.Equal(expectedBlock2Bytes.Length, block2Bytes.Length);
        Assert.Equal(expectedBlock2Bytes, block2Bytes);
        
        // assert base stream is unchanged
        var baseStreamBytes = new byte[Size];
        baseStream.Position = 0;
        baseStream.ReadExactly(baseStreamBytes, 0, Size);
        var expectedBaseStreamBytes = new byte[Size];
        Assert.Equal(expectedBaseStreamBytes.Length, baseStreamBytes.Length);
        Assert.Equal(expectedBaseStreamBytes, baseStreamBytes);
    }
    
    [Fact]
    public async Task When_WritingDataToSingleBlockAndFlushLayer_Then_DataIsWrittenFromLayerToBaseStream()
    {
        // arrange - base stream with size
        using var baseStream = new MemoryStream();
        baseStream.SetLength(Size);
        
        // arrange - empty layer stream
        using var layerStream = new MemoryStream();
        
        // arrange - layered stream on top of base and layer streams
        await using var layeredStream = new LayeredStream(baseStream, layerStream, Size, BlockSize);
        layeredStream.Initialize();
        
        // act - write data at position 50 to layered stream
        layeredStream.Position = 50;
        layeredStream.Write(blockTestDataBytes, 0, 4);
        await layeredStream.FlushLayer();
        
        // assert - layered stream structure is equal to expected size
        const int numberOfBlocks = 1;
        var expectedLayeredStreamSize = HeaderSize + BlockAllocationTableSize + (LongSize + IntSize + BlockSize) * numberOfBlocks;
        Assert.Equal(expectedLayeredStreamSize, layerStream.Length);
        
        // read block allocation table
        var blockAllocationTableBytes = new byte[BlockAllocationTableSize];
        layerStream.Position = HeaderSize;
        await layerStream.ReadExactlyAsync(blockAllocationTableBytes, 0, BlockAllocationTableSize);
        
        // create expected block allocation table
        var expectedBlockAllocationTableBytes = new byte[BlockAllocationTableSize];

        // write block number 0 offset in expected block allocation table position 0
        var blockOffsetBytes = BitConverter.GetBytes(Convert.ToInt64(HeaderSize + BlockAllocationTableSize));
        const int blockAllocationTableOffset = 0; // offset 0
        Array.Copy(blockOffsetBytes, 0, expectedBlockAllocationTableBytes, blockAllocationTableOffset, LongSize);
        
        // assert block allocation table
        Assert.Equal(expectedBlockAllocationTableBytes.Length, blockAllocationTableBytes.Length);
        Assert.Equal(expectedBlockAllocationTableBytes, blockAllocationTableBytes);
        
        // read block
        var block0Bytes = new byte[BlockHeaderSize + BlockSize];
        layerStream.Position = HeaderSize + BlockAllocationTableSize;
        await layerStream.ReadExactlyAsync(block0Bytes, 0, block0Bytes.Length);
        
        // assert block
        var expectedBlockBytes = new byte[BlockHeaderSize + BlockSize];
        Array.Copy(BitConverter.GetBytes(0L), 0, expectedBlockBytes, 0, LongSize); // block number
        Array.Copy(BitConverter.GetBytes(BlockSize), 0, expectedBlockBytes, 8, IntSize); // block size
        const int blockBytesOffset = 50;
        const int blockBytesToWrite = 4;
        Array.Copy(blockTestDataBytes, 0, expectedBlockBytes, BlockHeaderSize + blockBytesOffset, blockBytesToWrite); // block data
        Assert.Equal(expectedBlockBytes.Length, block0Bytes.Length);
        Assert.Equal(expectedBlockBytes, block0Bytes);
        
        // assert base stream is unchanged
        var baseStreamBytes = new byte[Size];
        baseStream.Position = 0;
        await baseStream.ReadExactlyAsync(baseStreamBytes, 0, Size);
        var expectedBaseStreamBytes = new byte[Size];
        Array.Copy(blockTestDataBytes, 0, expectedBaseStreamBytes, 50, 4);
        Assert.Equal(expectedBaseStreamBytes.Length, baseStreamBytes.Length);
        Assert.Equal(expectedBaseStreamBytes, baseStreamBytes);
    }
}