using System;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.ExFat;
using DiscUtils.Fat;
using DiscUtils.HfsPlus;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using Hst.Imager.Core.FileSystems.Ext;

namespace Hst.Imager.Core.PartitionTables;

public static class FileSystemReader
{
    public static async Task<Models.FileSystems.FileSystemInfo> ReadFileSystem(VirtualDisk disk, PartitionInfo partitionInfo)
    {
        var totalSectors = disk.Capacity / disk.SectorSize;

        if (partitionInfo.BiosType == BiosPartitionTypes.GptProtective)
        {
            return new Models.FileSystems.FileSystemInfo
            {
                FileSystemType = string.Empty
            };
        }

        if (partitionInfo.BiosType == Constants.BiosPartitionTypes.PiStormRdb)
        {
            return new Models.FileSystems.FileSystemInfo
            {
                FileSystemType = Constants.FileSystemNames.PiStormRdb,
                VolumeName = string.Empty,
                VolumeSize = 0,
                VolumeFree = 0,
                ClusterSize = 0
            };
        }

        if (disk.Geometry != null &&
            (partitionInfo.LastSector > totalSectors ||
             partitionInfo.FirstSector > totalSectors))
        {
            return new Models.FileSystems.FileSystemInfo
            {
                FileSystemType = "RAW"
            };
        }
        
        
        await using var stream = partitionInfo.Open();
        
        try
        {
            stream.Position = 0;

            if (FatFileSystem.Detect(stream))
            {
                var fatFileSystem = new FatFileSystem(stream);
                return new Models.FileSystems.FileSystemInfo
                {
                    FileSystemType = fatFileSystem.FatVariant.ToString().ToUpper(),
                    VolumeName = fatFileSystem.VolumeLabel,
                    VolumeSize = fatFileSystem.Size,
                    VolumeFree = fatFileSystem.Size - fatFileSystem.UsedSpace,
                    ClusterSize = fatFileSystem.ClusterSize
                };
            }
        }
        catch (Exception)
        {
            // ignored, if errors occur, not fat file system
        }

        try
        {
            stream.Position = 0;

            if (ExFatFileSystem.Detect(stream))
            {
                var exFatFileSystem = new ExFatFileSystem(stream);
                return new Models.FileSystems.FileSystemInfo
                {
                    FileSystemType = "exFAT",
                    VolumeName = exFatFileSystem.VolumeLabel,
                    VolumeSize = exFatFileSystem.Size,
                    VolumeFree = exFatFileSystem.Size - exFatFileSystem.UsedSpace,
                    ClusterSize = 0
                };
            }
        }
        catch (Exception)
        {
            // ignored, if errors occur, not exfat file system
        }

        try
        {
            stream.Position = 0;

            if (NtfsFileSystem.Detect(stream))
            {
                var ntfsFileSystem = new NtfsFileSystem(stream);
                return new Models.FileSystems.FileSystemInfo
                {
                    FileSystemType = "NTFS",
                    VolumeName = ntfsFileSystem.VolumeLabel,
                    VolumeSize = ntfsFileSystem.Size,
                    VolumeFree = ntfsFileSystem.Size - ntfsFileSystem.UsedSpace,
                    ClusterSize = ntfsFileSystem.ClusterSize
                };
            }
        }
        catch (Exception)
        {
            // ignored, if errors occur, not ntfs file system
        }

        try
        {
            stream.Position = 0;
            var hfsPlusFileSystem = new HfsPlusFileSystem(stream);
            return new Models.FileSystems.FileSystemInfo
            {
                FileSystemType = "Mac OS Extended",
                VolumeName = hfsPlusFileSystem.VolumeLabel,
                VolumeSize = hfsPlusFileSystem.Size,
                VolumeFree = hfsPlusFileSystem.Size - hfsPlusFileSystem.UsedSpace,
                ClusterSize = 0
            };
        }
        catch (Exception)
        {
            // ignored, if errors occur, not hfs plus file system
        }

        try
        {
            stream.Position = 0;
            var extFileSystemInfo = await ExtFileSystemReader.Read(stream);
            return new Models.FileSystems.FileSystemInfo
            {
                FileSystemType = extFileSystemInfo.Version.ToString().ToUpper(),
                VolumeName = extFileSystemInfo.VolumeName,
                VolumeSize = (long)extFileSystemInfo.Size,
                VolumeFree = (long)extFileSystemInfo.Free
            };
        }
        catch (Exception)
        {
            // ignored, if errors occur, not ext file system
        }

        return new Models.FileSystems.FileSystemInfo
        {
            FileSystemType = "RAW"
        };
    }
}