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
        void ClearActiveMedias();
        Result<Media> GetPhysicalDriveMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path, bool writeable = false);
        Result<Media> GetReadableFileMedia(string path);
        Result<Media> GetWritableFileMedia(string path, long? size = null, bool create = false);
        Result<Media> GetReadableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path);
        Result<Media> GetWritableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path, long? size = null,
            bool create = false);
        long GetVhdSize(long size);
        bool IsVhd(string path);
        Task<RigidDiskBlock> GetRigidDiskBlock(Stream stream);
        Task<DiskInfo> ReadDiskInfo(Media media);
        Result<MediaResult> ResolveMedia(string path);
    }
}