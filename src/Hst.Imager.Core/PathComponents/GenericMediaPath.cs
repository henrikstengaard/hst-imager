using System.IO;
using Hst.Imager.Core.Helpers;

namespace Hst.Imager.Core.PathComponents
{
    public class GenericMediaPath : IMediaPath
    {
        public char PathSeparator => Path.DirectorySeparatorChar;

        public string[] Split(string path) => PathHelper.Split(path);

        public string Join(string[] pathComponents) =>
            Path.Combine(pathComponents);
    }
}