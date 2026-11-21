using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;
using Hst.Imager.Core.PathComponents;

namespace Hst.Imager.Core.Volumes;

/// <summary>
/// Volume entry walker iterates through entries in a volume from root path components.
/// Last root path component can be a directory, a file or a pattern with wildcard, e.g. "*.txt".
/// Entries are iterated depth first with directories before files ordered by name.
/// </summary>
public class VolumeEntryWalker
{
    private readonly Stack<Entry> _nextEntries = new();
    private PathComponentMatcher _pathComponentMatcher;
    private bool _isFirst = true;
    private bool _initialized;

    /// <summary>
    /// Volume entry walker.
    /// </summary>
    /// <param name="volume">Volume to iterate entries in.</param>
    /// <param name="rootPathComponents">Root path components to iterate entries from.</param>
    /// <param name="recursive">Iterate entries recursively.</param>
    public VolumeEntryWalker(IVolume volume, string[] rootPathComponents, bool recursive)
    {
        Volume = volume;
        PathComponents = rootPathComponents;
        Recursive = recursive;
    }

    /// <summary>
    /// Volume to iterate entries in.
    /// </summary>
    public IVolume Volume { get; }

    /// <summary>
    /// Root path components of walker.
    /// </summary>
    public string[] PathComponents { get; }

    /// <summary>
    /// Dir path components from root path components that exist and is set during initialization.
    /// </summary>
    public string[] DirPathComponents { get; private set; } = [];

    /// <summary>
    /// Iterate entries recursively.
    /// </summary>
    public bool Recursive { get; }

    /// <summary>
    /// Current entry.
    /// </summary>
    public Entry Current { get; private set; }

    /// <summary>
    /// Has more entries to iterate.
    /// </summary>
    public bool HasMoreEntries => _nextEntries.Count > 0;

    /// <summary>
    /// Last root path component is a single existing file.
    /// </summary>
    public bool IsSingleFileEntryNext { get; private set; }

    /// <summary>
    /// Last root path component is a pattern with wildcard.
    /// </summary>
    public bool UsesPattern => _pathComponentMatcher?.UsesPattern ?? false;

    /// <summary>
    /// Create and initialize volume entry walker.
    /// </summary>
    public static async Task<Result<VolumeEntryWalker>> Create(IVolume volume, string[] rootPathComponents,
        bool recursive)
    {
        var walker = new VolumeEntryWalker(volume, rootPathComponents, recursive);
        var initializeResult = await walker.Initialize();
        return initializeResult.IsFaulted
            ? new Result<VolumeEntryWalker>(initializeResult.Error)
            : new Result<VolumeEntryWalker>(walker);
    }

    /// <summary>
    /// Initialize walker by resolving dir path components that exist from root path components.
    /// </summary>
    public async Task<Result> Initialize()
    {
        var dirComponents = 0;
        var usePattern = false;
        IsSingleFileEntryNext = false;

        for (var i = 0; i < PathComponents.Length; i++)
        {
            var pathComponent = PathComponents[i];
            var entryResult = await Volume.GetEntry(PathComponents.Take(i + 1).ToArray());

            var exists = entryResult.IsSuccess;
            var isDir = exists && entryResult.Value.IsDir();
            var isFile = exists && entryResult.Value.IsFile();

            // continue, if directory exists for path component
            if (isDir)
            {
                dirComponents++;
                continue;
            }

            // last path component is a file or a pattern
            if (dirComponents == PathComponents.Length - 1)
            {
                usePattern = true;
                IsSingleFileEntryNext = isFile;
                if (IsSingleFileEntryNext || PathComponentHelper.HasWildcard(pathComponent))
                {
                    break;
                }
            }

            var path = Volume.MediaPath.Join(PathComponents.Take(i + 1).ToArray());
            return new Result(new PathNotFoundError($"Path not found '{path}'", path));
        }

        DirPathComponents = PathComponents.Take(dirComponents).ToArray();
        _pathComponentMatcher = new PathComponentMatcher(usePattern ? PathComponents : [],
            isFile: IsSingleFileEntryNext, recursive: Recursive);
        _nextEntries.Clear();
        _isFirst = true;
        Current = null;
        _initialized = true;

        return new Result();
    }

    /// <summary>
    /// Move to next entry.
    /// </summary>
    /// <returns>True, if there is a next entry. Otherwise, false.</returns>
    public async Task<bool> Next()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Volume entry walker is not initialized");
        }

        if (_isFirst)
        {
            _isFirst = false;
            Current = null;
            await EnqueueDirectory(DirPathComponents);
        }

        while (_nextEntries.Count > 0)
        {
            var entry = _nextEntries.Pop();

            if (entry.Type == EntryType.File)
            {
                Current = entry;
                return true;
            }

            bool skipEntry;
            if (Recursive)
            {
                var entriesEnqueued = await EnqueueDirectory(entry.FullPathComponents);
                skipEntry = _pathComponentMatcher.UsesPattern && entriesEnqueued == 0;
            }
            else
            {
                skipEntry = entry.FullPathComponents.Length < _pathComponentMatcher.PathComponents.Length ||
                            !_pathComponentMatcher.IsMatch(entry.FullPathComponents);
            }

            if (skipEntry)
            {
                continue;
            }

            Current = entry;
            return true;
        }

        Current = null;
        return false;
    }

    /// <summary>
    /// Enqueue entries in directory with directories first followed by files ordered by name.
    /// </summary>
    /// <returns>Number of entries enqueued.</returns>
    private async Task<int> EnqueueDirectory(string[] dirPathComponents)
    {
        var entriesResult = await Volume.ListEntries(dirPathComponents);
        if (entriesResult.IsFaulted)
        {
            return 0;
        }

        var directories = new List<Entry>();
        var files = new List<Entry>();

        foreach (var volumeEntry in entriesResult.Value.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var fullPathComponents = volumeEntry.FullPathComponents;
            var relativePathComponents = EntryIteratorFunctions.GetRelativePathComponents(
                DirPathComponents, fullPathComponents).ToArray();
            var relativePath = Volume.MediaPath.Join(relativePathComponents);

            var entry = new Entry
            {
                Name = relativePath,
                FormattedName = relativePath,
                RawPath = volumeEntry.RawPath,
                FullPathComponents = fullPathComponents,
                RelativePathComponents = relativePathComponents,
                Date = volumeEntry.Date,
                Size = volumeEntry.Size,
                Type = volumeEntry.Type,
                Properties = volumeEntry.Properties
            };

            switch (entry.Type)
            {
                case EntryType.Dir:
                    directories.Add(entry);
                    break;
                case EntryType.File:
                    var isValid = EntryIteratorFunctions.IsRelativePathComponentsValid2(relativePathComponents,
                                      Recursive) &&
                                  _pathComponentMatcher.IsMatch(fullPathComponents);

                    // skip file entry, if it's not valid
                    if (isValid)
                    {
                        files.Add(entry);
                    }

                    break;
            }
        }

        for (var i = files.Count - 1; i >= 0; i--)
        {
            _nextEntries.Push(files[i]);
        }

        for (var i = directories.Count - 1; i >= 0; i--)
        {
            _nextEntries.Push(directories[i]);
        }

        return directories.Count + files.Count;
    }
}
