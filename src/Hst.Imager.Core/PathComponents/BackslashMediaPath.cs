using System;

namespace Hst.Imager.Core.PathComponents
{
    public class BackslashMediaPath : IMediaPath
    {
        public char PathSeparator => '\\';

        public string[] Split(string path) =>
            path.Split(new[] { PathSeparator }, StringSplitOptions.RemoveEmptyEntries);

        public string Join(string[] pathComponents) =>
            string.Join(PathSeparator.ToString(), pathComponents);
    }
}