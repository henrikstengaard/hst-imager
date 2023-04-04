namespace Hst.Imager.Core.Tests;

public class TestMedia
{
    public readonly string Path;
    public readonly string Name;
    public byte[] Data { get; private set; }

    public TestMedia(string path, string name, byte[] data)
    {
        Path = path;
        Name = name;
        Data = data;
    }

    public void SetData(byte[] data)
    {
        this.Data = data;
    }
}