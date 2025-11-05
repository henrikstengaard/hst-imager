using System.IO;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Core.IO;

namespace Hst.Imager.Core.Tests;

public class TestMedia
{
    public readonly string Path;
    public readonly string Name;
    public long Size => Stream.Length;
    public readonly BlockMemoryStream Stream;

    public TestMedia(string path, string name, long size)
    {
        Path = path;
        Name = name;
        Stream = new BlockMemoryStream();
        Stream.SetLength(size);
    }

    public async Task WriteData(byte[] data)
    {
        Stream.Seek(0, SeekOrigin.Begin);
        await Stream.WriteBytes(data);
        Stream.Seek(0, SeekOrigin.Begin);
    }

    public async Task<byte[]> ReadData()
    {
        Stream.Position = 0;
        return await Stream.ReadBytes((int)Stream.Length);
    }
}