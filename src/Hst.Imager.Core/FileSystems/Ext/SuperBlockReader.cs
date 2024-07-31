using System.IO;
using System.Text;

namespace Hst.Imager.Core.FileSystems.Ext;

public static class SuperBlockReader
{
    public static SuperBlock Read(byte[] blockBytes)
    {
        var magic = LittleEndianConverter.ConvertBytesToUInt16(blockBytes, 0x38);

        if (magic != 0xef53)
        {
            throw new IOException("Invalid magic signature");
        }
        
        var sInodesCount = LittleEndianConverter.ConvertBytesToUInt32(blockBytes, 0x0);
        var sBlocksCountLo = LittleEndianConverter.ConvertBytesToUInt32(blockBytes, 0x4);
        var srBlocksCountLo = LittleEndianConverter.ConvertBytesToUInt32(blockBytes, 0x8);
        var sFreeBlocksCountLo = LittleEndianConverter.ConvertBytesToUInt32(blockBytes, 0xc);

        var sFreeInodesCount = LittleEndianConverter.ConvertBytesToUInt32(blockBytes, 0x10);
        var sFirstDataBlock = LittleEndianConverter.ConvertBytesToUInt32(blockBytes, 0x14);
        var sLogBlockSize = LittleEndianConverter.ConvertBytesToUInt32(blockBytes, 0x18);
        
        var featureCompatible = LittleEndianConverter.ConvertBytesToUInt32(blockBytes, 0x5c);
        var featureIncompatible = LittleEndianConverter.ConvertBytesToUInt32(blockBytes, 0x60);
        var featureRoCompatible = LittleEndianConverter.ConvertBytesToUInt32(blockBytes, 0x64);

        var volumeName = Encoding.ASCII.GetString(blockBytes, 0x78, 16);
        
        var sBlocksCountHi = LittleEndianConverter.ConvertBytesToUInt32(blockBytes, 0x150);
        var srBlocksCountHi = LittleEndianConverter.ConvertBytesToUInt32(blockBytes, 0x154);
        var sFreeBlocksCountHi = LittleEndianConverter.ConvertBytesToUInt32(blockBytes, 0x158);

        return new SuperBlock
        {
            SInodesCount = sInodesCount,
            SBlocksCountLo = sBlocksCountLo,
            SrBlocksCountLo = srBlocksCountLo,
            SFreeBlocksCountLo = sFreeBlocksCountLo,
            SFreeInodesCount = sFreeInodesCount,
            SFirstDataBlock = sFirstDataBlock,
            SLogBlockSize = sLogBlockSize,
            Magic = magic,
            SFeatureCompat = featureCompatible,
            SFeatureIncompat = featureIncompatible,
            SFeatureRoCompat = featureRoCompatible,
            SBlocksCountHi = sBlocksCountHi,
            SrBlocksCountHi = srBlocksCountHi,
            SFreeBlocksCountHi = sFreeBlocksCountHi,
            VolumeName = volumeName
        };
    }
}