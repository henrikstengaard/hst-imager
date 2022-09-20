namespace HstWbInstaller.Imager.Core.Commands
{
    using Hst.Core;

    public class UnsupportedImageError : Error
    {
        public readonly string Path;

        public UnsupportedImageError(string path)
        {
            Path = path;
        }
    }
}