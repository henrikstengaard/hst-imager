using System;
using System.IO;
using Hst.Core.Extensions;

namespace Hst.Imager.Core;

public class MacOsMediaStream : MediaStream
{
    private readonly string path;

    public MacOsMediaStream(Stream stream, string path, long size) : base(stream, size)
    {
        this.path = path;
    }
    
    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                try
                {
                    "diskutil".RunProcess($"mountDisk {path}");
                }
                catch (Exception)
                {
                    // ignored, if mount disk fails
                }
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}