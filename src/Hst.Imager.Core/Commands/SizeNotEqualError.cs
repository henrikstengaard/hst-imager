namespace Hst.Imager.Core.Commands
{
    using Hst.Core;

    public class SizeNotEqualError : Error
    {
        /// <summary>
        /// offset where size started to differ
        /// </summary>
        public long Offset;

        public long CompareSize;

        /// <summary>
        /// expected size
        /// </summary>
        public long Size;
        
        public SizeNotEqualError(long offset, long compareSize, long size) 
            : base($"SizeNotEqualError at offset {offset} compare size is {compareSize} and size is {size}")
        {
            Offset = offset;
            CompareSize = compareSize;
            Size = size;
        }
    }
}