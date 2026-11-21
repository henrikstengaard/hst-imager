namespace Hst.Imager.Core.Tests;

using System.Threading.Tasks;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Medias;
using Hst.Imager.Core.Models;

/// <summary>
/// Disk info reader returning disk info with an overridden rigid disk block, if defined.
/// </summary>
public class TestDiskInfoReader : DiskInfoReader
{
    private readonly RigidDiskBlock rigidDiskBlock;

    public TestDiskInfoReader(RigidDiskBlock rigidDiskBlock = null)
    {
        this.rigidDiskBlock = rigidDiskBlock;
    }

    public override async Task<DiskInfo> ReadDiskInfo(Media media,
        PartitionTableType partitionTableTypeContext = PartitionTableType.None)
    {
        var diskInfo = await base.ReadDiskInfo(media, partitionTableTypeContext);
        if (rigidDiskBlock != null)
        {
            diskInfo.RigidDiskBlock = rigidDiskBlock;
        }

        return diskInfo;
    }
}
