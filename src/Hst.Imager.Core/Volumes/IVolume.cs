using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hst.Core;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.FileSystems;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.PathComponents;
using Hst.Imager.Core.UaeMetadatas;
using Entry = Hst.Imager.Core.Models.FileSystems.Entry;

namespace Hst.Imager.Core.Volumes;

/// <summary>
/// Volume giving read and write access to entries in a mounted file system, archive or local directory.
/// All paths are absolute path components from the root of the volume.
/// </summary>
public interface IVolume : IDisposable
{
    /// <summary>
    /// Media the volume is mounted from. Null for volumes not backed by media, e.g. archives.
    /// </summary>
    Media Media { get; }

    /// <summary>
    /// Partition table type the volume is mounted from.
    /// </summary>
    PartitionTableType PartitionTableType { get; }

    /// <summary>
    /// Partition number the volume is mounted from.
    /// </summary>
    int PartitionNumber { get; }

    /// <summary>
    /// Media path used to join and split path components for the volume.
    /// </summary>
    IMediaPath MediaPath { get; }

    /// <summary>
    /// Default attributes mode for entries in the volume.
    /// </summary>
    AttributesMode AttributesMode { get; }

    /// <summary>
    /// Volume is read-only and all write operations returns <see cref="VolumeReadOnlyError"/>.
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Volume supports UAE metadata.
    /// </summary>
    bool SupportsUaeMetadata { get; }

    /// <summary>
    /// UAE metadata used by volume.
    /// </summary>
    UaeMetadata UaeMetadata { get; set; }

    /// <summary>
    /// Get entry for path components. Empty path components returns root directory entry.
    /// </summary>
    /// <returns>Entry or <see cref="Commands.PathNotFoundError"/>, if entry doesn't exist.</returns>
    Task<Result<Entry>> GetEntry(string[] pathComponents);

    /// <summary>
    /// List entries in directory. Entry name is the name of the entry and full path components are set.
    /// </summary>
    Task<Result<IEnumerable<Entry>>> ListEntries(string[] dirPathComponents);

    /// <summary>
    /// Open file for reading.
    /// </summary>
    Task<Result<Stream>> OpenFile(string[] pathComponents);

    /// <summary>
    /// Create directory. Parent directory must exist. Succeeds if directory already exists.
    /// </summary>
    Task<Result> CreateDirectory(string[] pathComponents);

    /// <summary>
    /// Write file from stream. Parent directory must exist.
    /// </summary>
    Task<Result> WriteFile(string[] pathComponents, Stream stream, bool overwrite);

    /// <summary>
    /// Set entry information like attributes, protection bits, date and comment from entry properties.
    /// </summary>
    Task<Result> SetEntryInfo(string[] pathComponents, Entry entry, bool skipAttributes);

    /// <summary>
    /// Set attributes of entry by applying attributes change to current attributes of entry.
    /// </summary>
    /// <returns>Attributes set for entry.</returns>
    Task<Result<int>> SetAttributes(string[] pathComponents, AttributesChange attributesChange);

    /// <summary>
    /// Create link at path components to link path.
    /// </summary>
    Task<Result> CreateLink(string[] pathComponents, string linkPath);

    /// <summary>
    /// Delete file or directory.
    /// </summary>
    Task<Result> Delete(string[] pathComponents);

    /// <summary>
    /// Move file or directory to destination path components. Destination parent directory must exist.
    /// </summary>
    Task<Result> Move(string[] srcPathComponents, string[] destPathComponents, bool overwrite);

    /// <summary>
    /// Is same volume examines if other volume is mounted from same media and partition.
    /// </summary>
    bool IsSameVolume(IVolume other);

    /// <summary>
    /// Flush changes.
    /// </summary>
    Task Flush();

    /// <summary>
    /// Get debug logs from volume.
    /// </summary>
    IEnumerable<string> GetDebugLogs();

    /// <summary>
    /// Get logs from volume.
    /// </summary>
    IEnumerable<string> GetLogs();
}
