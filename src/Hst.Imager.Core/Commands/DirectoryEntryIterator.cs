namespace Hst.Imager.Core.Commands;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Models.FileSystems;

public class DirectoryEntryIterator : IEntryIterator
{
    private readonly string path;
    private readonly bool recursive;
    private readonly Queue<DirectoryInfo> dirs;
    private readonly Queue<FileInfo> files;
    private string currentPath;
    private Entry currentEntry;

    public DirectoryEntryIterator(string path, bool recursive)
    {
        this.path = path;
        this.recursive = recursive;
        this.dirs = new Queue<DirectoryInfo>();
        this.files = new Queue<FileInfo>();
    }

    public Entry Current => currentEntry;

    public Task<bool> Next()
    {
        // first time, current path is null and enqueue root path
        if (string.IsNullOrEmpty(currentPath))
        {
            currentEntry = null;
            currentPath = path;
            var currentDir = new DirectoryInfo(currentPath);
            EnqueueDir(currentDir);
        }

        // no more files left in queue, enqueue next directory
        if (this.files.Count == 0)
        {
            if (!this.recursive || this.dirs.Count == 0)
            {
                currentEntry = null;
                return Task.FromResult(false);
            }

            var nextDirInfo = this.dirs.Dequeue();
            var nextDirPath = Path.GetDirectoryName(nextDirInfo.FullName) ?? string.Empty;
            currentEntry = new Entry
            {
                Name = nextDirInfo.Name,
                Path = nextDirPath.Length >= this.path.Length + 1
                    ? nextDirPath.Substring(this.path.Length + 1)
                    : string.Empty,
                Date = nextDirInfo.LastWriteTime,
                Size = 0,
                Type = EntryType.Dir,
                Attributes = ""
            };
            EnqueueDir(nextDirInfo);
            return Task.FromResult(true);
        }

        // no more files, return null
        if (this.files.Count == 0)
        {
            currentEntry = null;
            return Task.FromResult(false);
        }

        var nextFileInfo = this.files.Dequeue();
        var nextEntryPath = Path.GetDirectoryName(nextFileInfo.FullName) ?? string.Empty;
        currentEntry = new Entry
        {
            Name = nextFileInfo.Name,
            Path = nextEntryPath.Length >= this.path.Length + 1
                ? nextEntryPath.Substring(this.path.Length + 1)
                : string.Empty,
            Date = nextFileInfo.LastWriteTime,
            Size = nextFileInfo.Length,
            Type = EntryType.File,
            Attributes = ""
        };

        return Task.FromResult(true);
    }

    private void EnqueueDir(DirectoryInfo currentDir)
    {
        if (this.recursive)
        {
            foreach (var dirInfo in currentDir.GetDirectories().OrderBy(x => x.Name).ToList())
            {
                this.dirs.Enqueue(dirInfo);
            }
        }

        foreach (var fileInfo in currentDir.GetFiles().OrderBy(x => x.Name).ToList())
        {
            this.files.Enqueue(fileInfo);
        }
    }

    public void Dispose()
    {
    }
}