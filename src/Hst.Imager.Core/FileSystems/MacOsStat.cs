using System;
#if OSX
using System.IO;
using System.Runtime.InteropServices;
#endif

namespace Hst.Imager.Core.FileSystems;

public class MacOsStat
{
#if OSX
    // macOS stat structure (64‑bit)
    // Based on /usr/include/sys/stat.h
    [StructLayout(LayoutKind.Sequential)]
    public struct Stat
    {
        public ulong st_dev;
        public ulong st_ino;
        public uint st_mode;
        public uint st_nlink;
        public uint st_uid;
        public uint st_gid;
        public ulong st_rdev;
        public long st_atime;
        public long st_atimensec;
        public long st_mtime;
        public long st_mtimensec;
        public long st_ctime;
        public long st_ctimensec;
        public long st_birthtime;
        public long st_birthtimensec;
        public ulong st_size;
        public ulong st_blocks;
        public ulong st_blksize;
        public uint st_flags;
        public uint st_gen;
        public int st_lspare;
        public long st_qspare1;
        public long st_qspare2;
    }

    // P/Invoke stat() from libSystem.dylib
    // macOS uses UTF‑8 for char* → string marshalling
    [LibraryImport("libSystem.dylib", EntryPoint = "stat",
        StringMarshalling = StringMarshalling.Utf8,
        SetLastError = true)]
    private static partial int stat(string path, out Stat buf);
#endif

    public static GenericFileInfo GetStat(string path)
    {
#if OSX
        try
        {
            var result = stat(path, out Stat st);

            if (result != 0)
            {
                var errno = Marshal.GetLastPInvokeError();
                throw new IOException($"stat failed (errno={errno})");
            }

            return new GenericFileInfo(path, (long)st.st_size, st.st_mode);
        }
        catch (Exception e)
        {
            throw new IOException($"Error: {e.Message}");
        }
#else
        throw new PlatformNotSupportedException();
#endif
    }
}