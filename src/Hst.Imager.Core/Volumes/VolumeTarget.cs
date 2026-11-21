using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;

namespace Hst.Imager.Core.Volumes;

/// <summary>
/// Volume target creates directories, files and moves entries in a volume relative to root path components.
/// Root path components must exist except the last one, which can be a directory or a file that either exists or doesn't exist.
/// - If the last root path component exists and is a directory, it's used as the root directory for creating directories and files.
/// - If the last root path component exists and is a file, then only a file entry can be created and will overwrite the file.
/// - If the last root path component does not exist, then only a file entry can be created as a new file.
/// </summary>
public class VolumeTarget
{
    private readonly bool _createDirectory;
    private readonly bool _forceOverwrite;
    private bool _lastPathComponentExist = true;
    private EntryType _lastPathComponentEntryType = EntryType.Dir;
    private bool _initialized;

    /// <summary>
    /// Volume target.
    /// </summary>
    /// <param name="volume">Volume to create directories and files in.</param>
    /// <param name="rootPathComponents">Root path components.</param>
    /// <param name="recursive">Recursive creating directories and files.</param>
    /// <param name="createDirectory">Create directories for root path components, if they don't exist.</param>
    /// <param name="forceOverwrite">Force overwriting existing files.</param>
    public VolumeTarget(IVolume volume, string[] rootPathComponents, bool recursive, bool createDirectory,
        bool forceOverwrite)
    {
        Volume = volume;
        PathComponents = rootPathComponents;
        Recursive = recursive;
        _createDirectory = createDirectory;
        _forceOverwrite = forceOverwrite;
    }

    /// <summary>
    /// Volume to create directories and files in.
    /// </summary>
    public IVolume Volume { get; }

    /// <summary>
    /// Root path components of target.
    /// </summary>
    public string[] PathComponents { get; }

    /// <summary>
    /// Dir path components of root path components that exist and is set during initialization.
    /// </summary>
    public string[] DirPathComponents { get; private set; } = [];

    /// <summary>
    /// Recursive creating directories and files.
    /// </summary>
    public bool Recursive { get; }

    /// <summary>
    /// Create and initialize volume target.
    /// </summary>
    public static async Task<Result<VolumeTarget>> Create(IVolume volume, string[] rootPathComponents,
        bool recursive, bool createDirectory, bool forceOverwrite)
    {
        var target = new VolumeTarget(volume, rootPathComponents, recursive, createDirectory, forceOverwrite);
        var initializeResult = await target.Initialize();
        return initializeResult.IsFaulted
            ? new Result<VolumeTarget>(initializeResult.Error)
            : new Result<VolumeTarget>(target);
    }

    private string JoinPath(IEnumerable<string> pathComponents) => Volume.MediaPath.Join(pathComponents.ToArray());

    /// <summary>
    /// Initialize the volume target verifying the root path components exist.
    /// </summary>
    public async Task<Result> Initialize()
    {
        if (Volume.IsReadOnly)
        {
            return VolumeEntryHelper.CreateReadOnlyResult(Volume, "Writing");
        }

        var existingPathComponents = new List<string>(10);
        _lastPathComponentExist = true;
        _lastPathComponentEntryType = EntryType.Dir;

        for (var i = 0; i < PathComponents.Length; i++)
        {
            var pathComponents = PathComponents.Take(i + 1).ToArray();
            var entryResult = await Volume.GetEntry(pathComponents);
            var isLast = i == PathComponents.Length - 1;

            if (entryResult.IsSuccess && !entryResult.Value.IsDir() && !isLast)
            {
                var path = JoinPath(pathComponents);
                return new Result(new PathNotFoundError($"Path '{path}' is a file and not a directory", path));
            }

            if (entryResult.IsFaulted)
            {
                if (!_createDirectory)
                {
                    if (!isLast)
                    {
                        var path = JoinPath(pathComponents);
                        return new Result(new PathNotFoundError($"Path not found '{path}'", path));
                    }

                    _lastPathComponentExist = false;
                    break;
                }

                var createDirectoryResult = await Volume.CreateDirectory(pathComponents);
                if (createDirectoryResult.IsFaulted)
                {
                    return createDirectoryResult;
                }
            }
            else if (isLast)
            {
                _lastPathComponentEntryType = entryResult.Value.IsDir() ? EntryType.Dir : EntryType.File;

                if (!entryResult.Value.IsDir())
                {
                    break;
                }
            }

            existingPathComponents.Add(PathComponents[i]);
        }

        if (Recursive && !_lastPathComponentExist)
        {
            var path = JoinPath(PathComponents);
            return new Result(new PathNotFoundError(
                $"Path '{path}' not found. Directory must exist when using recursive!", path));
        }

        DirPathComponents = existingPathComponents.ToArray();
        _initialized = true;

        return new Result();
    }

    private Result CheckInitialized() =>
        _initialized ? new Result() : new Result(new Error("Volume target is not initialized"));

    private string[] GetFullPathComponents(Entry entry, string[] entryPathComponents, bool isSingleEntry) =>
        PathComponentHelper.GetFullPathComponents(entry.Type, entryPathComponents, _lastPathComponentEntryType,
            PathComponents, _lastPathComponentExist, isSingleEntry);

    /// <summary>
    /// Ensure directories for dir path components exist. Required path components must exist,
    /// other directories are created.
    /// </summary>
    private async Task<Result> EnsureDirectories(string[] requiredPathComponents, string[] dirPathComponents)
    {
        for (var i = 1; i <= dirPathComponents.Length; i++)
        {
            var pathComponents = dirPathComponents.Take(i).ToArray();
            var entryResult = await Volume.GetEntry(pathComponents);

            if (entryResult.IsSuccess)
            {
                if (!entryResult.Value.IsDir())
                {
                    var path = JoinPath(pathComponents);
                    return new Result(new PathExistsError(
                        $"Create directory path '{path}' failed. Path already exists as a file!"));
                }

                continue;
            }

            if (i <= requiredPathComponents.Length)
            {
                var path = JoinPath(pathComponents);
                return new Result(new PathNotFoundError($"Path not found '{path}'", path));
            }

            var createDirectoryResult = await Volume.CreateDirectory(pathComponents);
            if (createDirectoryResult.IsFaulted)
            {
                return createDirectoryResult;
            }
        }

        return new Result();
    }

    /// <summary>
    /// Create directory for entry.
    /// </summary>
    /// <param name="entry">Entry to create directory for.</param>
    /// <param name="entryPathComponents">Entry path components relative to root path components.</param>
    /// <param name="skipAttributes">Skip setting attributes.</param>
    /// <param name="isSingleEntry">Is a single entry operation.</param>
    public async Task<Result> CreateDirectory(Entry entry, string[] entryPathComponents, bool skipAttributes,
        bool isSingleEntry)
    {
        var initializedResult = CheckInitialized();
        if (initializedResult.IsFaulted)
        {
            return initializedResult;
        }

        var fullPathComponents = GetFullPathComponents(entry, entryPathComponents, isSingleEntry);

        if (fullPathComponents.Length == 0)
        {
            return new Result();
        }

        var requiredPathComponents = isSingleEntry ? DirPathComponents : PathComponents;
        var ensureDirectoriesResult = await EnsureDirectories(requiredPathComponents, fullPathComponents[..^1]);
        if (ensureDirectoriesResult.IsFaulted)
        {
            return ensureDirectoriesResult;
        }

        var entryResult = await Volume.GetEntry(fullPathComponents);
        if (entryResult.IsSuccess && !entryResult.Value.IsDir())
        {
            return new Result(new PathExistsError(
                $"Path already exists with same name '{JoinPath(fullPathComponents)}'"));
        }

        if (entryResult.IsFaulted)
        {
            var createDirectoryResult = await Volume.CreateDirectory(fullPathComponents);
            if (createDirectoryResult.IsFaulted)
            {
                return createDirectoryResult;
            }
        }

        return await Volume.SetEntryInfo(fullPathComponents, entry, skipAttributes);
    }

    /// <summary>
    /// Create file for entry.
    /// </summary>
    /// <param name="entry">Entry to create file for.</param>
    /// <param name="entryPathComponents">Entry path components relative to root path components.</param>
    /// <param name="stream">Stream to write to file.</param>
    /// <param name="skipAttributes">Skip setting attributes.</param>
    /// <param name="isSingleEntry">Is a single entry operation.</param>
    public async Task<Result> CreateFile(Entry entry, string[] entryPathComponents, Stream stream,
        bool skipAttributes, bool isSingleEntry)
    {
        var initializedResult = CheckInitialized();
        if (initializedResult.IsFaulted)
        {
            return initializedResult;
        }

        var fullPathComponents = GetFullPathComponents(entry, entryPathComponents, isSingleEntry);

        if (fullPathComponents.Length == 0)
        {
            return new Result(new Error("Unable to create file without a name"));
        }

        var requiredPathComponents = isSingleEntry ? DirPathComponents : PathComponents;
        var ensureDirectoriesResult = await EnsureDirectories(requiredPathComponents, fullPathComponents[..^1]);
        if (ensureDirectoriesResult.IsFaulted)
        {
            return ensureDirectoriesResult;
        }

        var writeFileResult = await Volume.WriteFile(fullPathComponents, stream, _forceOverwrite);
        if (writeFileResult.IsFaulted)
        {
            return writeFileResult;
        }

        return await Volume.SetEntryInfo(fullPathComponents, entry, skipAttributes);
    }

    /// <summary>
    /// Move entry within volume to entry path components relative to root path components.
    /// </summary>
    /// <param name="entry">Entry to move, full path components is the source.</param>
    /// <param name="entryPathComponents">Entry path components relative to root path components.</param>
    /// <param name="isSingleEntry">Is a single entry operation.</param>
    public async Task<Result> MoveEntry(Entry entry, string[] entryPathComponents, bool isSingleEntry)
    {
        var initializedResult = CheckInitialized();
        if (initializedResult.IsFaulted)
        {
            return initializedResult;
        }

        var destFullPathComponents = GetFullPathComponents(entry, entryPathComponents, isSingleEntry);

        if (destFullPathComponents.Length == 0)
        {
            return new Result(new Error("Destination path is null or empty."));
        }

        var requiredPathComponents = isSingleEntry ? DirPathComponents : PathComponents;
        var ensureDirectoriesResult = await EnsureDirectories(requiredPathComponents, destFullPathComponents[..^1]);
        if (ensureDirectoriesResult.IsFaulted)
        {
            return ensureDirectoriesResult;
        }

        var srcEntryResult = await Volume.GetEntry(entry.FullPathComponents);
        if (srcEntryResult.IsFaulted)
        {
            var srcPath = JoinPath(entry.FullPathComponents);
            return new Result(new PathNotFoundError($"Source path '{srcPath}' not found", srcPath));
        }

        return await Volume.Move(entry.FullPathComponents, destFullPathComponents, _forceOverwrite);
    }

    /// <summary>
    /// Examines if walker and target path components results in a self copy.
    ///
    /// Examples of self copy paths:
    /// - dir1 -> dir1: Copy all files from dir1 to dir1.
    /// - dir1\* -> dir1: Copy all files from dir1 to dir1, same as not writing *.
    /// - dir1\file1.txt -> dir1: Copy single file file1.txt from dir1 to dir1.
    ///
    /// Examples of valid paths that are not self copy:
    /// - dir1\file1.txt -> dir2: Copy file1.txt from dir1 to dir2.
    /// - dir1\file1.txt -> dir1\file2.txt: Copy file1.txt from dir1 to file2.txt in dir1.
    /// </summary>
    /// <returns>True, if walker and target path components result in a self copy.</returns>
    public bool IsSelfCopy(VolumeEntryWalker walker)
    {
        // self copy is only possible, when copying from and to same volume
        if (!Volume.IsSameVolume(walker.Volume))
        {
            return false;
        }

        var sameDirPathComponents = walker.DirPathComponents.Length == DirPathComponents.Length &&
                                    walker.DirPathComponents.SequenceEqual(DirPathComponents,
                                        StringComparer.OrdinalIgnoreCase);

        // return false, if it's not same dir path components or if it's not a single file copy
        if (!sameDirPathComponents || !walker.IsSingleFileEntryNext)
        {
            return false;
        }

        var lastPathComponent = PathComponents.Length > 0 ? PathComponents[^1] : string.Empty;

        // return true, if last target path component is empty or if last target path component exist and
        // is same as last walker path component
        return string.IsNullOrEmpty(lastPathComponent) ||
               (_lastPathComponentExist && walker.PathComponents[^1].Equals(lastPathComponent));
    }

    /// <summary>
    /// Examines if walker and target path components results in a cyclic copy, where target is within walker.
    /// </summary>
    /// <returns>True, if walker and target path components result in a cyclic copy.</returns>
    public bool IsCyclic(VolumeEntryWalker walker)
    {
        // cyclic copy is only possible, when copying from and to same volume and recursive
        if (!Volume.IsSameVolume(walker.Volume) || !Recursive)
        {
            return false;
        }

        // array of path components that in length is the same between walker and target
        var sameDirPathComponents = walker.DirPathComponents.Length > 0 && DirPathComponents.Length > 1
            ? DirPathComponents.Take(walker.DirPathComponents.Length).ToArray()
            : [];

        // true, if target has same and more path components than walker
        return DirPathComponents.Length > walker.DirPathComponents.Length &&
               (walker.DirPathComponents.Length == 0 ||
                walker.DirPathComponents.SequenceEqual(sameDirPathComponents, StringComparer.OrdinalIgnoreCase));
    }
}
