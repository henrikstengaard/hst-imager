using System;
using Hst.Core.Converters;
using Hst.Imager.Core.FileSystems.Fat.Blocks;

namespace Hst.Imager.Core.FileSystems.Fat;

public static class BiosParameterBlockReader
{
    public static BiosParameterBlock Read(byte[] data, int offset = 0)
    {
        var jmpBoot = new byte[3];
        Array.Copy(data, offset, jmpBoot, 0, jmpBoot.Length);
        var oemName = new byte[8];
        Array.Copy(data, offset + 0x3, oemName, 0, oemName.Length);
        var bytesPerSector = LittleEndianConverter.ConvertBytesToUInt16(data, offset + 0xb);
        var sectorsPerCluster = data[offset + 0xd];
        var reservedSectorCount = LittleEndianConverter.ConvertBytesToUInt16(data, offset + 0xe);
        var numberOfFats = data[offset + 0x10];
        var rootEntriesCount = LittleEndianConverter.ConvertBytesToUInt16(data, offset + 0x11);
        var totalSectors = LittleEndianConverter.ConvertBytesToUInt16(data, offset + 0x13);
        var media = data[offset + 0x15];
        var fatSectors =  LittleEndianConverter.ConvertBytesToUInt16(data, offset + 0x16);
        var sectorsPerTrack =  LittleEndianConverter.ConvertBytesToUInt16(data, offset + 0x18);
        var numberOfHeads  = LittleEndianConverter.ConvertBytesToUInt16(data, offset + 0x1a);
        var hiddenSectors =  LittleEndianConverter.ConvertBytesToUInt32(data, offset + 0x1c);
        var totalSectorsFat32 = LittleEndianConverter.ConvertBytesToUInt32(data, offset + 0x20);

        return new BiosParameterBlock(jmpBoot, oemName, bytesPerSector, sectorsPerCluster, reservedSectorCount,
            numberOfFats, rootEntriesCount, totalSectors, media, fatSectors, sectorsPerTrack, numberOfHeads,
            hiddenSectors, totalSectorsFat32);
    }
}