namespace Hst.Imager.Core.Commands
{
    using System;

    public class RdbInfoReadEventArgs : EventArgs
    {
        public RdbInfo RdbInfo;

        public RdbInfoReadEventArgs(RdbInfo rdbInfo)
        {
            this.RdbInfo = rdbInfo;
        }
    }
}