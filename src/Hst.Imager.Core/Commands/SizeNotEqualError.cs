namespace Hst.Imager.Core.Commands
{
    using Hst.Core;

    public class SizeNotEqualError : Error
    {
        /// <summary>
        /// offset where size started to differ
        /// </summary>
        public long Offset;
        
        /// <summary>
        /// expected size
        /// </summary>
        public long Size;
        
        public SizeNotEqualError(long offset, long size) : base("SizeNotEqualError")
        {
            Offset = offset;
            Size = size;
        }
    }
}