using System;
using Hst.Imager.Core.MagicBytes;

namespace Hst.Imager.Core.Commands
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Hst.Core;
    using Medias;
    using Models;

    public interface ICommandHelper : IDisposable
    {
        /// <summary>
        /// Media manager used to open medias for read and write.
        /// </summary>
        IMediaManager MediaManager { get; }

        /// <summary>
        /// Path resolver used to resolve media path, file system path and modifiers.
        /// </summary>
        IMediaPathResolver PathResolver { get; }

        /// <summary>
        /// Disk info reader used to read partition tables and disk parts from medias.
        /// </summary>
        IDiskInfoReader DiskInfoReader { get; }

        Task<DataType> DetectDataType(string path);
        void ClearActiveMedias();
        void ClearActiveMedia(string path);
        void ClearActivePhysicalDrives();
        Task<Result<Media>> GetPhysicalDriveMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path, ModifierEnum? modifiers = null, bool writeable = false);
        Task<Result<Media>> GetReadableFileMedia(string path, ModifierEnum? modifiers = null);
        Task<Result<Media>> GetWritableFileMedia(string path, ModifierEnum? modifiers = null, long? size = null, bool create = false);
        Task<Result<Media>> GetReadableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path, ModifierEnum? modifiers = null);
        Task<Result<Media>> GetWritableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path, ModifierEnum? modifiers = null,
            long? size = null, bool create = false);
        Task<Result<Media>> CreateSparseFileMedia(string path, long size);
        long GetVhdSize(long size);
        bool IsZip(string path);
        bool IsGZip(string path);
        Task<DiskInfo> ReadDiskInfo(Media media,
            PartitionTableType partitionTableTypeContext = PartitionTableType.None);
        Result<MediaResult> ResolveMedia(string path, bool allowNonExisting = false);
        Task RescanPhysicalDrives();
    }
}