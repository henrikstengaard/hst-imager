namespace Hst.Imager.Core.Medias
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Hst.Core;
    using MagicBytes;
    using Models;

    /// <summary>
    /// Opens medias for read and write from physical drives and files (raw, vhd and compressed). Opened medias
    /// are kept as active medias, so the same media is reused instead of being opened twice.
    /// </summary>
    public interface IMediaManager : IDisposable
    {
        Task<DataType> DetectDataType(string path);
        void ClearActiveMedias();
        void ClearActiveMedia(string path);
        void ClearActivePhysicalDrives();
        Task<Result<Media>> GetPhysicalDriveMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            ModifierEnum? modifiers = null, bool writeable = false);
        Task<Result<Media>> GetReadableFileMedia(string path, ModifierEnum? modifiers = null);
        Task<Result<Media>> GetWritableFileMedia(string path, ModifierEnum? modifiers = null, long? size = null,
            bool create = false);
        Task<Result<Media>> GetReadableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            ModifierEnum? modifiers = null);
        Task<Result<Media>> GetWritableMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
            ModifierEnum? modifiers = null, long? size = null, bool create = false);
        Task<Result<Media>> CreateSparseFileMedia(string path, long size);
        Task RescanPhysicalDrives();
    }
}
