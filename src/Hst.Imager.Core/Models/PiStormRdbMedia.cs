namespace Hst.Imager.Core.Models
{
    using DiscUtils;
    using Hst.Amiga.RigidDiskBlocks;
    using System;
    using System.IO;

    /// <summary>
    /// PiStorm RDB media represents a Master Boot Record partition containing a Rigid Disk Block
    /// </summary>
    public class PiStormRdbMedia : Media
    {
        private readonly Media baseMedia;

        public PiStormRdbMedia(string path, string name, long size, MediaType type,
            bool isPhysicalDrive, Stream stream, bool byteswap, Media baseMedia)
            : base(path, name, size, type, isPhysicalDrive, stream, byteswap)
        {
            this.baseMedia = baseMedia;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                baseMedia.Dispose();
            }
        }
    }
}