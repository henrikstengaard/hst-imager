using System;

namespace Hst.Imager.Core.Commands
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Hst.Core;
    using Models;

    public interface ICommandHelper : IDisposable
    {
        void ClearActiveMedias();
        void ClearActivePhysicalDrives();
        Task<Result<Media>> GetPhysicalDriveMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path, ModifierEnum? modifiers = null, bool writeable = false);
        Task<Result<Media>> GetReadableFileMedia(string path, ModifierEnum? modifiers = null);
        Task<Result<Media>> GetWritableFileMedia(string path, ModifierEnum? modifiers = null, long? size = null, bool create = false);
        Task<Result<Media>> GetReadableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path, ModifierEnum? modifiers = null);
        Task<Result<Media>> GetWritableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path, ModifierEnum? modifiers = null,
            long? size = null, bool create = false);
        long GetVhdSize(long size);
        bool IsVhd(string path);
        bool IsZip(string path);
        bool IsGZip(string path);
        Task<DiskInfo> ReadDiskInfo(Media media,
            PartitionTableType partitionTableTypeContext = PartitionTableType.None);
        Result<MediaResult> ResolveMedia(string path);
    }
}