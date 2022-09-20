namespace Hst.Imager.Core.Commands
{
    using System;

    public class InfoReadEventArgs : EventArgs
    {
        public MediaInfo MediaInfo;
        public DiskInfo DiskInfo;

        public InfoReadEventArgs(MediaInfo mediaInfo, DiskInfo diskInfo)
        {
            this.MediaInfo = mediaInfo;
            DiskInfo = diskInfo;
        }
    }
}