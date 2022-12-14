namespace Hst.Imager.Core.Commands;

using System.IO;
using System.Threading.Tasks;
using Amiga.FileSystems;
using Entry = Models.FileSystems.Entry;

public class VolumeEntryWriter : IEntryWriter
{
    private readonly IFileSystemVolume volume;

    public VolumeEntryWriter(IFileSystemVolume volume)
    {
        this.volume = volume;
    }

    public Task Write(Entry entry, Stream stream)
    {
        throw new System.NotImplementedException();
    }
}