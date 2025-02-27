using System;
using System.IO;

namespace Hst.Imager.Core.PathComponents
{
    public class GenericMediaPath : IMediaPath
    {
        public char PathSeparator => Path.DirectorySeparatorChar;

        public string[] Split(string path) =>
            path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

        public string Join(string[] pathComponents) =>
            Path.Combine(pathComponents);
    }
}