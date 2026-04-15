using System;
using System.Linq;

namespace Hst.Imager.Core.PathComponents
{
    /// <summary>
    /// Forward slash media path that splits and joins paths using backslash as separator.
    /// </summary>
    public class ForwardSlashMediaPath : IMediaPath
    {
        public char PathSeparator => '/';

        public string[] Split(string path) =>
            (path.StartsWith(PathSeparator) ? new []{PathSeparator.ToString()} : Array.Empty<string>())
            .Concat(path.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries)).ToArray();

        public string Join(string[] pathComponents) =>
            pathComponents.Length > 0 && pathComponents[0] == PathSeparator.ToString()
                ? string.Concat(PathSeparator, string.Join(PathSeparator.ToString(), pathComponents.Skip(1)))
                : string.Join(PathSeparator.ToString(), pathComponents);
    }
}
