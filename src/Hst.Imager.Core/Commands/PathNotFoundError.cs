namespace Hst.Imager.Core.Commands
{
    using Hst.Core;

    public class PathNotFoundError : Error
    {
        public readonly string Path;
        
        public PathNotFoundError(string message, string path) : base(message)
        {
            Path = path;
        }
    }
}