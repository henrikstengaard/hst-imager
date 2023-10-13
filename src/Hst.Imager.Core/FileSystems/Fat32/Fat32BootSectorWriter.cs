using System;
using Hst.Imager.Core.Extensions;

namespace Hst.Imager.Core.FileSystems.Fat32;

public static class Fat32BootSectorWriter
{
    public static byte[] Build(Fat32BootSector fat32BootSector, int sectorSize)
    {
        var sectorBytes = new byte[sectorSize];
        if (fat32BootSector.SectorBytes != null)
        {
            Array.Copy(fat32BootSector.SectorBytes, 0, sectorBytes, 0, sectorSize);
        }

        Array.Copy(fat32BootSector.sJmpBoot, 0, sectorBytes, 0x0, fat32BootSector.sJmpBoot.Length);
        Array.Copy(fat32BootSector.sOEMName, 0, sectorBytes, 0x3, fat32BootSector.sOEMName.Length);
        sectorBytes.ConvertUInt16ToBytes(0xb, fat32BootSector.wBytsPerSec);
        sectorBytes[0xd] = fat32BootSector.bSecPerClus;
        sectorBytes.ConvertUInt16ToBytes(0xe, fat32BootSector.wRsvdSecCnt);
        sectorBytes[0x10] = fat32BootSector.bNumFATs;
        sectorBytes.ConvertUInt16ToBytes(0x11, fat32BootSector.wRootEntCnt);
        sectorBytes.ConvertUInt16ToBytes(0x13, fat32BootSector.wTotSec16);
        sectorBytes[0x15] = fat32BootSector.bMedia;
        sectorBytes.ConvertUInt16ToBytes(0x16, fat32BootSector.wFATSz16);
        sectorBytes.ConvertUInt16ToBytes(0x18, fat32BootSector.wSecPerTrk);
        sectorBytes.ConvertUInt16ToBytes(0x1a, fat32BootSector.wNumHeads);
        sectorBytes.ConvertUInt32ToBytes(0x1c, fat32BootSector.dHiddSec);
        sectorBytes.ConvertUInt32ToBytes(0x20, fat32BootSector.dTotSec32);
        sectorBytes.ConvertUInt32ToBytes(0x24, fat32BootSector.dFATSz32);
        sectorBytes.ConvertUInt16ToBytes(0x28, fat32BootSector.wExtFlags);
        sectorBytes.ConvertUInt16ToBytes(0x2a, fat32BootSector.wFSVer);
        sectorBytes.ConvertUInt32ToBytes(0x2c, fat32BootSector.dRootClus);
        sectorBytes.ConvertUInt16ToBytes(0x30, fat32BootSector.wFSInfo);
        sectorBytes.ConvertUInt16ToBytes(0x32, fat32BootSector.wBkBootSec);
        Array.Copy(fat32BootSector.Reserved, 0, sectorBytes, 0x34, fat32BootSector.Reserved.Length);
        sectorBytes[0x40] = fat32BootSector.bDrvNum;
        sectorBytes[0x41] = fat32BootSector.Reserved1;
        sectorBytes[0x42] = fat32BootSector.bBootSig;
        sectorBytes.ConvertUInt32ToBytes(0x43, fat32BootSector.dBS_VolID);
        Array.Copy(fat32BootSector.sVolLab, 0, sectorBytes, 0x47, fat32BootSector.sVolLab.Length);
        Array.Copy(fat32BootSector.sBS_FilSysType, 0, sectorBytes, 0x52, fat32BootSector.sBS_FilSysType.Length);
        Array.Copy(fat32BootSector.ExecutableCode, 0, sectorBytes, 0x5a, fat32BootSector.ExecutableCode.Length);
        Array.Copy(fat32BootSector.BootRecordSignature, 0, sectorBytes, 0x1fe, fat32BootSector.BootRecordSignature.Length);

        /* FATGEN103.DOC says "NOTE: Many FAT documents mistakenly say that this 0xAA55 signature occupies the "last 2 bytes of
        the boot sector". This statement is correct if - and only if - BPB_BytsPerSec is 512. If BPB_BytsPerSec is greater than
        512, the offsets of these signature bytes do not change (although it is perfectly OK for the last two bytes at the end
        of the boot sector to also contain this signature)."

        Windows seems to only check the bytes at offsets 510 and 511. Other OSs might check the ones at the end of the sector,
        so we'll put them there too.
        */

        if (sectorSize != 512)
        {
            Array.Copy(fat32BootSector.BootRecordSignature, 0, sectorBytes, sectorSize - 2, fat32BootSector.BootRecordSignature.Length);
        }

        return sectorBytes;
    }
}