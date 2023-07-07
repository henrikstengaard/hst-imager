namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Models.FileSystems;

public class DirectoryEntryWriter : IEntryWriter
{
    private readonly string path;
    private readonly byte[] buffer;
    private readonly bool isWindowsOperatingSystem;
    private readonly IList<string> logs;

    public DirectoryEntryWriter(string path)
    {
        this.path = path;
        this.buffer = new byte[4096];
        this.isWindowsOperatingSystem = OperatingSystem.IsWindows();
        this.logs = new List<string>();
    }

    public string MediaPath => this.path;
    public string FileSystemPath => string.Empty;
    
    public void Dispose()
    {
    }

    // \, /, :, *, ?, ", <, >, |, #
    private static readonly Regex InvalidFilenameCharsRegex = new Regex("[\\\\/:\\*\\?\"\\<\\>\\|#]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private string GetEntryPath(string[] entryPathComponents)
    {
        var entryPath = Path.Combine(entryPathComponents);

        if (!entryPathComponents.Any(x => InvalidFilenameCharsRegex.IsMatch(x)))
        {
            return Path.Combine(path, entryPath);
        }

        var newEntryPath =
            Path.Combine(entryPathComponents.Select(x => InvalidFilenameCharsRegex.Replace(x, "_")).ToArray());
        logs.Add($"{entryPath} -> {newEntryPath}");
        entryPath = newEntryPath;

        return Path.Combine(path, entryPath);
    }

    public Task CreateDirectory(Entry entry, string[] entryPathComponents, bool skipAttributes)
    {
        var entryPath = GetEntryPath(entryPathComponents);
        if (!string.IsNullOrEmpty(entryPath) && !Directory.Exists(entryPath))
        {
            Directory.CreateDirectory(entryPath);
        }

        return Task.CompletedTask;
    }

    public async Task WriteEntry(Entry entry, string[] entryPathComponents, Stream stream, bool skipAttributes)
    {
        var entryPath = GetEntryPath(entryPathComponents);
        var dirPath = Path.GetDirectoryName(entryPath) ?? string.Empty;

        if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }

        var fileName = Path.GetFileNameWithoutExtension(entryPath);
        if (isWindowsOperatingSystem && Regexs.WindowsReservedNamesRegex.IsMatch(fileName))
        {
            var newFileName = $".{fileName}{Path.GetExtension(entryPath)}";
            entryPath = Path.Combine(dirPath, newFileName);

            logs.Add($"{entryPath} -> {newFileName}");
        }

        await using var fileStream = File.Open(entryPath, FileMode.Create, FileAccess.ReadWrite);

        int bytesRead;
        do
        {
            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            await fileStream.WriteAsync(buffer, 0, bytesRead);
        } while
            (bytesRead !=
             0); // continue until bytes read is 0. reads from zip streams can return bytes between 0 to buffer length. 

        fileStream.Close();
        await fileStream.DisposeAsync();

        if (entry.Date.HasValue)
        {
            File.SetCreationTime(entryPath, entry.Date.Value);
            File.SetLastWriteTime(entryPath, entry.Date.Value);
            File.SetLastAccessTime(entryPath, entry.Date.Value);
        }
    }

    public Task Flush()
    {
        return Task.CompletedTask;
    }

    public IEnumerable<string> GetDebugLogs()
    {
        return new List<string>();
    }

    public IEnumerable<string> GetLogs()
    {
        return this.logs.Count == 0
            ? new List<string>()
            : new[]
            {
                string.Empty, "Following files were renamed to due invalid characters or conflicts with Windows OS reserved filenames:"
            }.Concat(logs);
    }

    public IEntryIterator CreateEntryIterator(string rootPath, bool recursive) => null;
}