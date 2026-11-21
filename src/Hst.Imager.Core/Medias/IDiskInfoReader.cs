namespace Hst.Imager.Core.Medias
{
    using System.Threading.Tasks;
    using Commands;
    using Models;

    /// <summary>
    /// Reads disk info with partition tables and disk parts from media.
    /// </summary>
    public interface IDiskInfoReader
    {
        Task<DiskInfo> ReadDiskInfo(Media media,
            PartitionTableType partitionTableTypeContext = PartitionTableType.None);
    }
}
