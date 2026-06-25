using System;
using Hst.Core.Converters;
using Hst.Imager.Core.FileSystems.Fat.Blocks;

namespace Hst.Imager.Core.FileSystems.Fat;

public static class ExtendedBiosParameterBlockReader
{
    public static ExtendedBiosParameterBlock Read(byte[] data, int offset = 0)
    {
        var driveNumber = data[offset + 0x24];
        var reserved1 = data[offset + 0x25];
        var bootSignature = data[offset + 0x26];
        
        if (bootSignature != 0x29)
        {
            return new ExtendedBiosParameterBlock(0, 0, 0, 0, 0,
                0, new byte[12], driveNumber, reserved1, bootSignature, 0,
                new byte[11], new byte[8]);
        }
        
        var volumeId = LittleEndianConverter.ConvertBytesToUInt32(data, offset + 0x27);
        var volumeLabel = new byte[11];
        Array.Copy(data, offset + 0x2b, volumeLabel, 0, volumeLabel.Length);
        var fileSystemType = new byte[8];
        Array.Copy(data, offset + 0x36, fileSystemType, 0, fileSystemType.Length);

        return new ExtendedBiosParameterBlock(0, 0, 0, 0, 0,
            0, new byte[12], driveNumber, reserved1, bootSignature, volumeId, volumeLabel,
            fileSystemType);
    }
}