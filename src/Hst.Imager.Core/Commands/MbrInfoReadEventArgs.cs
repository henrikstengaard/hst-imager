namespace Hst.Imager.Core.Commands
{
    using System;

    public class MbrInfoReadEventArgs : EventArgs
    {
        public MbrInfo MbrInfo;

        public MbrInfoReadEventArgs(MbrInfo mbrInfo)
        {
            this.MbrInfo = mbrInfo;
        }
    }
}