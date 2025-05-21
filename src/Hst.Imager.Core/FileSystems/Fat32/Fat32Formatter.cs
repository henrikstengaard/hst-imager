using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Extensions;

namespace Hst.Imager.Core.FileSystems.Fat32;

public static class Fat32Formatter
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="partitionOffset">Partition offset in stream</param>
    /// <param name="size">Size of partition in bytes</param>
    /// <param name="bytesPerSector"></param>
    /// <param name="sectorsPerTrack"></param>
    /// <param name="tracksPerCylinder"></param>
    /// <param name="volumeLabel"></param>
    /// <param name="clusterSize">Cluster size in bytes (0 = optimal cluster size)</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="IOException"></exception>
    public static async Task FormatPartition(Stream stream, long partitionOffset, long size, int bytesPerSector,
        int sectorsPerTrack, int tracksPerCylinder, string volumeLabel, int clusterSize = 0)
    {
        if (volumeLabel == null) throw new ArgumentNullException(nameof(volumeLabel));

        if (clusterSize < 0 || clusterSize > 65536 || clusterSize % 512 != 0)
        {
            throw new ArgumentOutOfRangeException(
                $"Invalid cluster size {clusterSize}. Must be 512-65536 and dividable by 512");
        }
        
        var volumeId = GetVolumeId();

        var sectorCount = size / bytesPerSector;

        switch (sectorCount)
        {
            case < 65536:
                // fail, if less than 65536 sectors required by fat32 specification
                throw new IOException($"Partition has {sectorCount} sectors and FAT32 requires a minimum of 65536 sectors");
            case >= 0xffffffff:
                // fail, if more than 4294967295 sectors (32bit) sectors supported by fat32 specification
                throw new IOException( $"Partition has {sectorCount} sectors and FAT32 only supports a maximum of {(long)0xffffffff} sectors");
        }

        const uint reservedSectorCount = 32;
        const uint numberOfFats = 2;
        const uint backupBootSector = 6;

        var sectorsPerCluster = (byte)(clusterSize > 0 
            ? clusterSize / 512
            : CalculateOptimalSectorsPerCluster(size, (uint)bytesPerSector));

        var fatSizeInSectors = CalculateFatSizeSectors((uint)sectorCount, reservedSectorCount, 
            sectorsPerCluster, numberOfFats, (uint)bytesPerSector);
        
        // fat 32 boot sector
        var fat32BootSector = new Fat32BootSector
        {
            sJmpBoot =
            {
                [0] = 0xeb,
                [1] = 0x58,
                [2] = 0x90
            },
            sOEMName = Encoding.ASCII.GetBytes("MSWIN4.1"),
            wBytsPerSec = (ushort)bytesPerSector,
            bSecPerClus = sectorsPerCluster,
            wRsvdSecCnt = (ushort)reservedSectorCount,
            bNumFATs = (byte)numberOfFats,
            wRootEntCnt = 0,
            wTotSec16 = 0,
            bMedia = 0xF8,
            wFATSz16 = 0,
            wSecPerTrk = (ushort)sectorsPerTrack,
            wNumHeads = (ushort)tracksPerCylinder,
            dHiddSec = (uint)(partitionOffset / bytesPerSector),
            dTotSec32 = (uint)sectorCount,
            dFATSz32 = fatSizeInSectors,
            wExtFlags = 0,
            wFSVer = 0,
            dRootClus = 2,
            wFSInfo = 1,
            wBkBootSec = (ushort)backupBootSector,
            bDrvNum = 0x80,
            Reserved1 = 0,
            bBootSig = 0x29,
            dBS_VolID = volumeId,
            sVolLab = Encoding.ASCII.GetBytes(volumeLabel.Length > 11 
                ? volumeLabel.Substring(0, 11) 
                : volumeLabel),
            sBS_FilSysType = Encoding.ASCII.GetBytes("FAT32   "),
            ExecutableCode = new byte[420],
            BootRecordSignature = new byte[] { 0x55, 0xaa }
        };

        // fat file system info
        var fatFsInfo = new FatFsInfo
        {
            dLeadSig = 0x41615252,
            dStrucSig = 0x61417272,
            dFree_Count = uint.MaxValue,
            dNxt_Free = uint.MaxValue,
            dTrailSig = 0xaa550000,
            BootRecordSignature = new byte[] { 0x55, 0xaa }
        };

        // first fat sector bytes
        var firstFatSectorBytes = new byte[bytesPerSector];
        firstFatSectorBytes.ConvertUInt32ToBytes(0x0, 0x0ffffff8); // reserved cluster 1 media id in low byte
        firstFatSectorBytes.ConvertUInt32ToBytes(0x4, 0xffffffff); // reserved cluster 2 root dir
        firstFatSectorBytes.ConvertUInt32ToBytes(0x8, 0x0fffffff); // end of cluster chain for root dir

        var systemAreaSize = (reservedSectorCount + (numberOfFats * fatSizeInSectors) + fat32BootSector.bSecPerClus);
        var dataAreaSize = sectorCount - reservedSectorCount - numberOfFats * fatSizeInSectors;    
        var clusterCount = dataAreaSize / fat32BootSector.bSecPerClus;

        // fail, if partition has more than 0x0fffffff (2^28) clusters. upper 4 bits of the cluster values in the FAT are reserved.
        if (clusterCount > 0x0fffffff)
        {
            throw new IOException( $"Partition has more than {clusterCount} clusters and FAT32 support a maximum of {0x0fffffff} clusters. Try with a a larger cluster size or use the optimal cluster size");
        }

        // fail, if partition has less than 65536 clusters. might get detected as FAT16B
        if (clusterCount < 65536)
        {
            throw new IOException( $"Partition only has {clusterCount} clusters and FAT32 requires a minimum of 65536 clusters. Try with a smaller cluster size or use optimal cluster size");
        }

        // calculate fat sectors required
        var fatSectorsRequired = clusterCount * 4;
        fatSectorsRequired += bytesPerSector - 1;
        fatSectorsRequired /= bytesPerSector;
        
        // fail, if partition requires more fat tables, than fat size can fit
        if (fatSectorsRequired > fatSizeInSectors)
        {
            throw new IOException( $"Partition size {size} is too large to handle");
        }
        
        // update fat file system info sector with number of free clusters and next free cluster
        fatFsInfo.dFree_Count = (uint)(dataAreaSize / fat32BootSector.bSecPerClus - 1);
        fatFsInfo.dNxt_Free = 3; // clusters 0-1 are reserved, cluster 2 is used for the root dir

        // build fat32 boot sector and fat fs info sector bytes
        var fat32BootSectorBytes = Fat32BootSectorWriter.Build(fat32BootSector, bytesPerSector);
        var fatFsInfoBytes = FatFsInfoWriter.Build(fatFsInfo, bytesPerSector);

        // write zero bytes to system area sectors
        await WriteZeroSectors(stream, partitionOffset, 0, bytesPerSector, (int)systemAreaSize);

        // write fat32 boot sector and fat fs info sector at sector 0 and backup boot sector
        foreach (var sectorStart in new[]{ 0U, backupBootSector})
        {
            stream.Seek(partitionOffset + (sectorStart * bytesPerSector), SeekOrigin.Begin);
            await stream.WriteBytes(fat32BootSectorBytes);

            stream.Seek(partitionOffset + ((sectorStart + 1) * bytesPerSector), SeekOrigin.Begin);
            await stream.WriteBytes(fatFsInfoBytes);
        }

        // write first fat sector for fat tables
        for (var i = 0; i < numberOfFats; i++ )
        {
            var sectorStart = reservedSectorCount + (i * fatSizeInSectors);
            stream.Seek(partitionOffset + (sectorStart * bytesPerSector), SeekOrigin.Begin);
            await stream.WriteBytes(firstFatSectorBytes);
        }

        // create volume id entry
        var volumeIdEntry = new Fat32Entry
        {
            Name = volumeLabel,
            Attribute = 8,
            CreationDate = DateTime.Now
        };

        // build volume id entry bytes
        var volumeIdEntryBytes = Fat32EntryWriter.Build(volumeIdEntry);

        // calculate root directory location
        var rootDirSector = reservedSectorCount + (numberOfFats * fatSizeInSectors);

        // write volume id entry
        stream.Seek(partitionOffset + (rootDirSector * bytesPerSector), SeekOrigin.Begin);
        await stream.WriteBytes(volumeIdEntryBytes);
    }
    
    private static async Task WriteZeroSectors(Stream stream, long partitionOffset, uint startSector, int bytesPerSector, 
        int sectors)
    {
        var burstSize = 128; // 64K

        var zeroBytes = new byte[bytesPerSector * burstSize];

        var offset = startSector * bytesPerSector;
        while (sectors > 0)
        {
            stream.Seek(partitionOffset + offset, SeekOrigin.Begin);

            var writeSectors = sectors > burstSize ? burstSize : sectors;
            var writeBytes = writeSectors * bytesPerSector;
            await stream.WriteAsync(zeroBytes, 0, writeBytes);

            offset += writeBytes;
            sectors -= writeSectors;
        }
    }
    
    /*
28.2  CALCULATING THE VOLUME SERIAL NUMBER

For example, say a disk was formatted on 26 Dec 95 at 9:55 PM and 41.94
seconds.  DOS takes the date and time just before it writes it to the
disk.

Low order word is calculated:               Volume Serial Number is:
    Month & Day         12/26   0c1ah
    Sec & Hundrenths    41:94   295eh               3578:1d02
                                -----
                                3578h

High order word is calculated:
    Hours & Minutes     21:55   1537h
    Year                1995    07cbh
                                -----
                                1d02h
*/
    private static uint GetVolumeId()
    {
        var now = DateTime.Now;

        var lo = now.Day + ( now.Month << 8 );
        var tmp = (now.Millisecond/10) + (now.Second << 8 );
        lo += tmp;

        var hi = now.Minute + ( now.Hour << 8 );
        hi += now.Year;
   
        return (uint)lo + ((uint)hi << 16);
    }
    
    public static uint CalculateFatSizeSectors(uint sectorCount, uint reservedSectorCount, uint sectorsPerCluster,
        uint numFats, uint bytesPerSector)
    {
        const ulong fatElementSize = 4;

        var numerator = fatElementSize * (sectorCount - reservedSectorCount);
        var denominator = (sectorsPerCluster * bytesPerSector) + (fatElementSize * numFats);
        var fatSize = (numerator / denominator) + 1;

        return (uint)fatSize;
    }

    /// <summary>
    /// Calculate sectors per cluster
    /// </summary>
    /// <param name="clusterSizeKb">Cluster size in kb</param>
    /// <param name="bytesPerSector"></param>
    /// <returns></returns>
    private static byte CalculateSectorsPerCluster(uint clusterSizeKb, uint bytesPerSector)
    {
        return (byte)((clusterSizeKb * 1024) / bytesPerSector);
    }
    
    /// <summary>
    /// Calculate optimal sectors per cluster
    /// </summary>
    /// <param name="diskSizeBytes"></param>
    /// <param name="bytesPerSector"></param>
    /// <returns></returns>
    private static byte CalculateOptimalSectorsPerCluster(long diskSizeBytes, uint bytesPerSector)
    {
        byte ret = 0x01; // 1 sector per cluster
        var diskSizeMb = diskSizeBytes / ( 1024*1024 );

        // 512 MB to 8,191 MB 4 KB
        if ( diskSizeMb > 512 )
            ret = CalculateSectorsPerCluster( 4, bytesPerSector );  // ret = 0x8;
        
        // 8,192 MB to 16,383 MB 8 KB 
        if ( diskSizeMb > 8192 )
            ret = CalculateSectorsPerCluster( 8, bytesPerSector ); // ret = 0x10;

        // 16,384 MB to 32,767 MB 16 KB 
        if ( diskSizeMb > 16384 )
            ret = CalculateSectorsPerCluster( 16, bytesPerSector ); // ret = 0x20;

        // Larger than 32,768 MB 32 KB
        if ( diskSizeMb > 32768 )
            ret = CalculateSectorsPerCluster( 32, bytesPerSector );  // ret = 0x40;
    
        return ret;
    }
}