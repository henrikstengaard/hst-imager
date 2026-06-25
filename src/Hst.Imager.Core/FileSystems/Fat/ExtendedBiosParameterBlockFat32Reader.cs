using System;
using Hst.Core.Converters;
using Hst.Imager.Core.FileSystems.Fat.Blocks;

namespace Hst.Imager.Core.FileSystems.Fat;

public static class ExtendedBiosParameterBlockFat32Reader
{
    public static ExtendedBiosParameterBlock Read(byte[] data, int offset = 0)
    {
        var fatSectorsFat32 = LittleEndianConverter.ConvertBytesToUInt32(data, offset + 0x24);
        var extFlags = LittleEndianConverter.ConvertBytesToUInt16(data, offset + 0x28);
        var fsVersion = LittleEndianConverter.ConvertBytesToUInt16(data, offset + 0x2a);
        var rootCluster = LittleEndianConverter.ConvertBytesToUInt32(data, offset + 0x2c);
        var fsInfoSector = LittleEndianConverter.ConvertBytesToUInt16(data, offset + 0x30);
        var backupBootSector = LittleEndianConverter.ConvertBytesToUInt16(data, offset + 0x32);
        var reserved = new byte[12];
        Array.Copy(data, offset + 0x34, reserved, 0, reserved.Length);
        var driveNumber = data[offset + 0x40];
        var reserved1 = data[offset + 0x41];
        var bootSignature = data[offset + 0x42];
        
        if (bootSignature != 0x29)
        {
            return new ExtendedBiosParameterBlock(fatSectorsFat32, extFlags, fsVersion, rootCluster, fsInfoSector,
                backupBootSector, reserved, driveNumber, reserved1, bootSignature, 0, new byte[11],
                new byte[8]);
        }
        
        var volumeId = LittleEndianConverter.ConvertBytesToUInt32(data, offset + 0x43);
        var volumeLabel = new byte[11];
        Array.Copy(data, offset + 0x47, volumeLabel, 0, volumeLabel.Length);
        var fileSystemType = new byte[8];
        Array.Copy(data, offset + 0x52, fileSystemType, 0, fileSystemType.Length);

        return new ExtendedBiosParameterBlock(fatSectorsFat32, extFlags, fsVersion, rootCluster, fsInfoSector,
            backupBootSector, reserved, driveNumber, reserved1, bootSignature, volumeId, volumeLabel, fileSystemType);
    }
}