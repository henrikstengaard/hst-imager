namespace Hst.Imager.Core.Tests;

using System;

public class TestMedia
{
    public readonly string Path;
    public readonly string Name;
    public long Size { get; private set; }
    public byte[] Data { get; private set; }

    public TestMedia(string path, string name, long size, byte[] data = null)
    {
        Path = path;
        Name = name;
        Size = size;
        Data = data ?? Array.Empty<byte>();
    }

    public void SetSize(long size)
    {
        this.Size = size;
    }
    
    public void SetData(byte[] data)
    {
        this.Data = data;
    }
}