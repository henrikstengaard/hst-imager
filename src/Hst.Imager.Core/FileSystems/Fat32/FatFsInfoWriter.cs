using System;
using Hst.Imager.Core.Extensions;

namespace Hst.Imager.Core.FileSystems.Fat32;

public static class FatFsInfoWriter
{
    public static byte[] Build(FatFsInfo fatFsInfo, int sectorSize)
    {
        var sectorBytes = new byte[sectorSize];
        if (fatFsInfo.SectorBytes != null)
        {
            Array.Copy(fatFsInfo.SectorBytes, 0, sectorBytes, 0, sectorSize);
        }

        sectorBytes.ConvertUInt32ToBytes(0x0, fatFsInfo.dLeadSig);
        Array.Copy(fatFsInfo.sReserved1, 0, sectorBytes, 0x4, fatFsInfo.sReserved1.Length);
        sectorBytes.ConvertUInt32ToBytes(0x1e4, fatFsInfo.dStrucSig);
        sectorBytes.ConvertUInt32ToBytes(0x1e8, fatFsInfo.dFree_Count);
        sectorBytes.ConvertUInt32ToBytes(0x1ec, fatFsInfo.dNxt_Free);
        Array.Copy(fatFsInfo.sReserved2, 0, sectorBytes, 0x1f0, fatFsInfo.sReserved2.Length);
        sectorBytes.ConvertUInt32ToBytes(0x1fc, fatFsInfo.dTrailSig);
        Array.Copy(fatFsInfo.BootRecordSignature, 0, sectorBytes, 0x1fe, fatFsInfo.BootRecordSignature.Length);

        if (sectorSize != 512)
        {
            Array.Copy(fatFsInfo.BootRecordSignature, 0, sectorBytes, sectorSize - 2, fatFsInfo.BootRecordSignature.Length);
        }

        return sectorBytes;
    }
}