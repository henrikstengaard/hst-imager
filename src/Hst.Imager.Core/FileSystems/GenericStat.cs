using System.IO;

namespace Hst.Imager.Core.FileSystems;

public static class GenericStat
{
    public static GenericFileInfo GetStat(string path)
    {
#if Windows
        var fileInfo = new FileInfo(path);
        //fileInfo.UnixFileMode
        return new GenericFileInfo(path, fileInfo.Length, 0);
#elif Linux
        return LinuxStat.GetStat(path);
#elif OSX
        return MacOsStat.GetStat(path);
#endif
    }
}