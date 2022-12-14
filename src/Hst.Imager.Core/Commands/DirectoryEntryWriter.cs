namespace Hst.Imager.Core.Commands;

using System.IO;
using System.Threading.Tasks;
using Models.FileSystems;

public class DirectoryEntryWriter : IEntryWriter
{
    private readonly string path;
    private readonly byte[] buffer;

    public DirectoryEntryWriter(string path)
    {
        this.path = path;
        this.buffer = new byte[1024 * 32];
    }

    public async Task Write(Entry entry, Stream stream)
    {
        var entryPath = Path.Combine(path, entry.Path, entry.Name)
            .Replace("\\", Path.DirectorySeparatorChar.ToString())
            .Replace("/", Path.DirectorySeparatorChar.ToString());
        
        if (entry.Type == EntryType.Dir)
        {
            if (!Directory.Exists(entryPath))
            {
                Directory.CreateDirectory(entryPath);
            }

            return;
        }
        
        await using var fileStream =
            File.Open(Path.Combine(path, entry.Path, entry.Name), FileMode.Create, FileAccess.ReadWrite);
        int bytesRead;
        do
        {
            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            fileStream.Write(buffer, 0, bytesRead);
        } while (bytesRead == buffer.Length);
    }
}