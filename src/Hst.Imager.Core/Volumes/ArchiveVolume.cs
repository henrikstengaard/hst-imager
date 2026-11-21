using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.FileSystems;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.PathComponents;
using Hst.Imager.Core.UaeMetadatas;
using Entry = Hst.Imager.Core.Models.FileSystems.Entry;
using EntryType = Hst.Imager.Core.Models.FileSystems.EntryType;

namespace Hst.Imager.Core.Volumes;

/// <summary>
/// Archive item added to archive volume.
/// </summary>
/// <param name="PathComponents">Path components of item in archive.</param>
/// <param name="IsDir">Item is a directory.</param>
/// <param name="Date">Date of item.</param>
/// <param name="Size">Size of item.</param>
/// <param name="Properties">Properties of item.</param>
/// <param name="RawPath">Raw path of item in archive.</param>
/// <param name="Open">Function to open item for reading.</param>
public record ArchiveItem(
    string[] PathComponents,
    bool IsDir,
    DateTime Date,
    long Size,
    IDictionary<string, string> Properties,
    string RawPath,
    Func<Task<Stream>> Open);

/// <summary>
/// Read-only volume for archives (zip, lha, lzx, lzw). Archive items are indexed into a directory tree,
/// which also creates directories implicitly defined by item paths.
/// </summary>
public class ArchiveVolume : IVolume
{
    private class Node
    {
        public Entry Entry { get; init; }
        public Func<Task<Stream>> Open { get; init; }
        public Dictionary<string, Node> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly Node _root;
    private readonly IDisposable _disposable;
    private bool _disposed;

    /// <summary>
    /// Archive volume.
    /// </summary>
    /// <param name="mediaPath">Media path used to split and join paths in archive.</param>
    /// <param name="attributesMode">Default attributes mode of archive.</param>
    /// <param name="supportsUaeMetadata">Archive supports UAE metadata.</param>
    /// <param name="items">Archive items.</param>
    /// <param name="disposable">Disposable disposed together with volume, e.g. archive or stream.</param>
    public ArchiveVolume(IMediaPath mediaPath, AttributesMode attributesMode, bool supportsUaeMetadata,
        IEnumerable<ArchiveItem> items, IDisposable disposable = null)
    {
        MediaPath = mediaPath;
        AttributesMode = attributesMode;
        SupportsUaeMetadata = supportsUaeMetadata;
        _disposable = disposable;
        _root = new Node { Entry = VolumeEntryHelper.CreateRootEntry() };

        foreach (var item in items)
        {
            AddItem(item);
        }
    }

    public Media Media => null;
    public PartitionTableType PartitionTableType => PartitionTableType.None;
    public int PartitionNumber => 0;
    public IMediaPath MediaPath { get; }
    public AttributesMode AttributesMode { get; }
    public bool IsReadOnly => true;
    public bool SupportsUaeMetadata { get; }
    public UaeMetadata UaeMetadata { get; set; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposable?.Dispose();
        _disposed = true;
    }

    private void AddItem(ArchiveItem item)
    {
        if (item.PathComponents.Length == 0)
        {
            return;
        }

        var node = _root;
        for (var i = 0; i < item.PathComponents.Length; i++)
        {
            var name = item.PathComponents[i];
            var isLast = i == item.PathComponents.Length - 1;
            var fullPathComponents = item.PathComponents.Take(i + 1).ToArray();

            if (node.Children.TryGetValue(name, out var child))
            {
                // replace implicit directory with archive directory item
                if (isLast && item.IsDir && child.Entry.Type == EntryType.Dir)
                {
                    child.Entry.Date = item.Date;
                    child.Entry.RawPath = item.RawPath;
                    child.Entry.Properties = item.Properties ?? new Dictionary<string, string>();
                }

                node = child;
                continue;
            }

            var isDir = !isLast || item.IsDir;
            child = new Node
            {
                Entry = new Entry
                {
                    Name = name,
                    FormattedName = name,
                    RawPath = isLast ? item.RawPath : MediaPath.Join(fullPathComponents),
                    FullPathComponents = fullPathComponents,
                    RelativePathComponents = [name],
                    Date = isLast ? item.Date : DateTime.Now,
                    Size = isDir ? 0 : item.Size,
                    Type = isDir ? EntryType.Dir : EntryType.File,
                    Properties = isLast && item.Properties != null
                        ? item.Properties
                        : new Dictionary<string, string>()
                },
                Open = isLast && !item.IsDir ? item.Open : null
            };
            node.Children[name] = child;
            node = child;
        }
    }

    private Node FindNode(string[] pathComponents)
    {
        var node = _root;
        foreach (var pathComponent in pathComponents)
        {
            if (!node.Children.TryGetValue(pathComponent, out node))
            {
                return null;
            }
        }

        return node;
    }

    private static Entry CopyEntry(Entry entry) => new()
    {
        Name = entry.Name,
        FormattedName = entry.FormattedName,
        RawPath = entry.RawPath,
        FullPathComponents = entry.FullPathComponents.ToArray(),
        RelativePathComponents = entry.RelativePathComponents.ToArray(),
        Date = entry.Date,
        Size = entry.Size,
        Type = entry.Type,
        Properties = new Dictionary<string, string>(entry.Properties)
    };

    private PathNotFoundError CreatePathNotFoundError(string[] pathComponents)
    {
        var path = MediaPath.Join(pathComponents);
        return new PathNotFoundError($"Path not found '{path}'", path);
    }

    public Task<Result<Entry>> GetEntry(string[] pathComponents)
    {
        var node = FindNode(pathComponents);
        return Task.FromResult(node == null
            ? new Result<Entry>(CreatePathNotFoundError(pathComponents))
            : new Result<Entry>(CopyEntry(node.Entry)));
    }

    public Task<Result<IEnumerable<Entry>>> ListEntries(string[] dirPathComponents)
    {
        var node = FindNode(dirPathComponents);
        if (node == null || node.Entry.Type != EntryType.Dir)
        {
            return Task.FromResult(new Result<IEnumerable<Entry>>(CreatePathNotFoundError(dirPathComponents)));
        }

        return Task.FromResult(new Result<IEnumerable<Entry>>(node.Children.Values
            .Select(x => CopyEntry(x.Entry)).ToList()));
    }

    public async Task<Result<Stream>> OpenFile(string[] pathComponents)
    {
        var node = FindNode(pathComponents);
        if (node?.Open == null)
        {
            return new Result<Stream>(CreatePathNotFoundError(pathComponents));
        }

        return new Result<Stream>(await node.Open());
    }

    public Task<Result> CreateDirectory(string[] pathComponents) =>
        Task.FromResult(VolumeEntryHelper.CreateReadOnlyResult(this, "Create directory"));

    public Task<Result> WriteFile(string[] pathComponents, Stream stream, bool overwrite) =>
        Task.FromResult(VolumeEntryHelper.CreateReadOnlyResult(this, "Write file"));

    public Task<Result> SetEntryInfo(string[] pathComponents, Entry entry, bool skipAttributes) =>
        Task.FromResult(VolumeEntryHelper.CreateReadOnlyResult(this, "Set entry info"));

    public Task<Result<int>> SetAttributes(string[] pathComponents, AttributesChange attributesChange) =>
        Task.FromResult(VolumeEntryHelper.CreateReadOnlyResult<int>(this, "Set attributes"));

    public Task<Result> CreateLink(string[] pathComponents, string linkPath) =>
        Task.FromResult(VolumeEntryHelper.CreateReadOnlyResult(this, "Create link"));

    public Task<Result> Delete(string[] pathComponents) =>
        Task.FromResult(VolumeEntryHelper.CreateReadOnlyResult(this, "Delete"));

    public Task<Result> Move(string[] srcPathComponents, string[] destPathComponents, bool overwrite) =>
        Task.FromResult(VolumeEntryHelper.CreateReadOnlyResult(this, "Move"));

    public bool IsSameVolume(IVolume other) => ReferenceEquals(this, other);

    public Task Flush() => Task.CompletedTask;

    public IEnumerable<string> GetDebugLogs() => [];

    public IEnumerable<string> GetLogs() => [];
}
