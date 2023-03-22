namespace Hst.Imager.Core.Commands
{
    using Hst.Core;

    public class UnsupportedImageError : Error
    {
        public readonly string Path;

        public UnsupportedImageError(string path) : base($"Unsupported image '{path}'")
        {
            Path = path;
        }
    }
}