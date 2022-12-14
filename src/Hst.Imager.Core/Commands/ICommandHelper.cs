namespace Hst.Imager.Core.Commands
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Hst.Amiga.RigidDiskBlocks;
    using Hst.Core;
    using Models;

    public interface ICommandHelper
    {
        Result<Media> GetReadableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path, bool allowPhysicalDrive = true);
        Result<Media> GetWritableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path, long? size = null,
            bool allowPhysicalDrive = true, bool create = false);
        long GetVhdSize(long size);
        bool IsVhd(string path);
        Task<RigidDiskBlock> GetRigidDiskBlock(Stream stream);
        Task<DiskInfo> ReadDiskInfo(Media media, Stream stream);
    }
}