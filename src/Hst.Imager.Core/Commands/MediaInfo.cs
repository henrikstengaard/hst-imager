namespace Hst.Imager.Core.Commands
{
    using Models;

    public class MediaInfo
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public bool IsPhysicalDrive;
        public Media.MediaType Type;        
        public long DiskSize { get; set; }

        /// <summary>
        /// Disk size is unknown, e.g. gzip compressed image where uncompressed size is unknown. Disk size is 0.
        /// </summary>
        public bool IsDiskSizeUnknown { get; set; }
        public DiskInfo DiskInfo { get; set; }
        public bool Byteswap { get; set; }
        public bool SystemDrive { get; set; }
    }
}