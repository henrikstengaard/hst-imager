using System;
using System.Threading.Tasks;
using DiscUtils.Fat;
using DiscUtils.HfsPlus;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using Hst.Imager.Core.FileSystems.Ext;
using Hst.Imager.Core.Models.FileSystems;

namespace Hst.Imager.Core.PartitionTables;

public static class FileSystemReader
{
    public static async Task<FileSystemInfo> ReadFileSystem(PartitionInfo partitionInfo)
    {
        if (partitionInfo.BiosType == BiosPartitionTypes.GptProtective)
        {
            return new FileSystemInfo
            {
                FileSystemType = string.Empty
            };
        }

        await using var stream = partitionInfo.Open();
        
        try
        {
            stream.Position = 0;
            var fatFileSystem = new FatFileSystem(stream);
            return new FileSystemInfo
            {
                FileSystemType = fatFileSystem.FileSystemType.ToUpper(),
                VolumeName = fatFileSystem.VolumeLabel,
                VolumeSize = fatFileSystem.Size,
                VolumeFree = fatFileSystem.Size - fatFileSystem.UsedSpace,
                ClusterSize = fatFileSystem.ClusterSize
            };
        }
        catch (Exception)
        {
            // ignored, if errors occur. not ext file system
        }

        try
        {
            stream.Position = 0;
            var ntfsFileSystem = new NtfsFileSystem(stream);
            return new FileSystemInfo
            {
                FileSystemType = "NTFS",
                VolumeName = ntfsFileSystem.VolumeLabel,
                VolumeSize = ntfsFileSystem.Size,
                VolumeFree = ntfsFileSystem.Size - ntfsFileSystem.UsedSpace,
                ClusterSize = ntfsFileSystem.ClusterSize
            };
        }
        catch (Exception)
        {
            // ignored, if errors occur. not ext file system
        }

        try
        {
            stream.Position = 0;
            var hfsPlusFileSystem = new HfsPlusFileSystem(stream);
            return new FileSystemInfo
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
            // ignored, if errors occur. not ext file system
        }

        try
        {
            stream.Position = 0;
            var extFileSystemInfo = await ExtFileSystemReader.Read(stream);
            return new FileSystemInfo
            {
                FileSystemType = extFileSystemInfo.Version.ToString().ToUpper(),
                VolumeName = extFileSystemInfo.VolumeName,
                VolumeSize = (long)extFileSystemInfo.Size,
                VolumeFree = (long)extFileSystemInfo.Free
            };
        }
        catch (Exception)
        {
            // ignored, if errors occur. not ext file system
        }

        return new FileSystemInfo
        {
            FileSystemType = "RAW"
        };
    }
}