using System;
using System.Collections.Generic;
using Hst.Amiga.VersionStrings;

namespace Hst.Imager.Core.Commands
{
    public class FileSystemVersionComparer : IComparer<Tuple<string, byte[]>>
    {
        public int Compare(Tuple<string, byte[]> x, Tuple<string, byte[]> y)
        {
            if (x == null || y == null || x.Item2 == null || y.Item2 == null)
            {
                throw new ArgumentException("Arguments cannot be null");
            }

            return CompareVersion(x.Item2, y.Item2);
        }

        private static int CompareVersion(byte[] x, byte[] y)
        {
            var xVersion = VersionStringReader.Read(x);
            var yVersion = VersionStringReader.Read(y);

            var xAmigaVersion = string.IsNullOrEmpty(xVersion)
                ? new AmigaVersion { Version = 0, Revision = 0 }
                : VersionStringReader.Parse(xVersion);
            var yAmigaVersion = string.IsNullOrEmpty(yVersion)
                ? new AmigaVersion { Version = 0, Revision = 0 }
                : VersionStringReader.Parse(yVersion);

            return xAmigaVersion.Version == yAmigaVersion.Version
                ? xAmigaVersion.Revision.CompareTo(yAmigaVersion.Revision)
                : xAmigaVersion.Version.CompareTo(yAmigaVersion.Version);
        }
    }
}