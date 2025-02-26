using System;
using System.IO;
using Hst.Core.Extensions;

namespace Hst.Imager.Core;

/// <summary>
/// MacOS physical drive media stream used close and dispose it's stream and
// mount physical drive when disposed.
/// </summary>
public class MacOsMediaStream : MediaStream
{
    private readonly string path;

    private bool isDisposed;

    public MacOsMediaStream(Stream stream, string path, long size) : base(stream, size)
    {
        this.path = path;
        this.isDisposed = false;
    }
    
    protected override void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }

        if (disposing)
        {
            base.Dispose(disposing);
            Stream?.Close();
            Stream?.Dispose();

            try
            {
                // use diskutil to mount disk at path
                "diskutil".RunProcess($"mountDisk {path}");
            }
            catch (Exception)
            {
                // ignored, if mount disk fails
            }
        }

        isDisposed = true;
    }
}