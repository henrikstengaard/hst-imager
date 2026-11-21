namespace Hst.Imager.Core.Medias
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Commands;
    using DiscUtils;
    using Helpers;
    using Models;
    using PartitionTables;
    using SparseFiles;

    public class DiskInfoReader : IDiskInfoReader
    {
        public virtual async Task<DiskInfo> ReadDiskInfo(Media media,
            PartitionTableType partitionTableTypeContext = PartitionTableType.None)
        {
            if (media.Size == 0)
            {
                return new DiskInfo
                {
                    Path = media.Path,
                    Name = media.Model,
                    Size = 0,
                    PartitionTables = new List<PartitionTableInfo>(),
                    StartOffset = 0,
                    EndOffset = 0,
                    RigidDiskBlock = null,
                    DiskParts = new List<PartInfo>()
                };
            }

            var isSparseFile = false;
            var sparseFileSize = 0L;
            if (!media.IsPhysicalDrive && File.Exists(media.Path))
            {
                try
                {
                    isSparseFile = SparseFile.IsSparseFile(media.Path);
                    sparseFileSize = SparseFile.GetSparseFileSize(media.Path);
                }
                catch (Exception)
                {
                    isSparseFile = false;
                }
            }

            var partitionTables = new List<PartitionTableInfo>();

            var disk = await MediaHelper.ResolveVirtualDisk(media);

            PartitionTableInfo mbrPartitionTableInfo = null;

            var biosPartitionTable = MbrPartitionTableReader.Read(disk);
            if (biosPartitionTable != null)
            {
                mbrPartitionTableInfo = await MbrPartitionTableReader.Read(disk, biosPartitionTable);
                partitionTables.Add(mbrPartitionTableInfo);
            }

            PartitionTableInfo guidPartitionTableInfo = null;

            var guidPartitionTable = GuidPartitionTableReader.Read(disk);
            if (guidPartitionTable != null)
            {
                guidPartitionTableInfo = await GuidPartitionTableReader.Read(disk, guidPartitionTable);
                partitionTables.Add(guidPartitionTableInfo);
            }

            PartitionTableInfo rdbPartitionTableInfo = null;

            global::Hst.Amiga.RigidDiskBlocks.RigidDiskBlock rigidDiskBlock = null;
            try
            {
                disk.Content.Position = 0;
                rigidDiskBlock = await global::Hst.Amiga.RigidDiskBlocks.RigidDiskBlockReader.Read(disk.Content);
                if (rigidDiskBlock != null)
                {
                    rdbPartitionTableInfo = PartitionTables.RigidDiskBlockReader.Read(disk, rigidDiskBlock);
                    partitionTables.Add(rdbPartitionTableInfo);
                }
            }
            catch (Exception)
            {
                // ignored, if read rigid disk block fails
            }

            var diskInfo = new DiskInfo
            {
                Path = media.Path,
                Name = media.Model,
                Size = GetDiskSize(media, disk),
                IsSparseFile = isSparseFile,
                SparseFileSize = sparseFileSize,
                PartitionTables = partitionTables,
                StartOffset = 0,
                EndOffset = media.Size > 0 ? media.Size - 1 : 0,
                RigidDiskBlock = rigidDiskBlock,
            };

            diskInfo.GptPartitionTablePart = DiskPartLayoutBuilder.CreatePartitionTablePart(diskInfo,
                guidPartitionTableInfo, partitionTableTypeContext);
            diskInfo.MbrPartitionTablePart = DiskPartLayoutBuilder.CreatePartitionTablePart(diskInfo,
                mbrPartitionTableInfo, partitionTableTypeContext);
            diskInfo.RdbPartitionTablePart = DiskPartLayoutBuilder.CreatePartitionTablePart(diskInfo,
                rdbPartitionTableInfo, partitionTableTypeContext);
            diskInfo.DiskParts = DiskPartLayoutBuilder.CreateDiskParts(diskInfo, partitionTableTypeContext).ToList();

            return diskInfo;
        }

        private static long GetDiskSize(Media media, VirtualDisk disk)
        {
            return media.Type switch
            {
                Media.MediaType.CompressedVhd => disk.Capacity,
                _ => media.Size
            };
        }
    }
}
