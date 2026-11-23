using System;
using System.Collections.Generic;
using Hst.Core;
using Hst.Imager.Core.Models.FileSystems;
using Hst.Imager.Core.PathComponents;

namespace Hst.Imager.Core.Volumes;

public static class VolumeEntryHelper
{
    /// <summary>
    /// Create root directory entry.
    /// </summary>
    public static Entry CreateRootEntry() => new()
    {
        Name = string.Empty,
        FormattedName = string.Empty,
        RawPath = string.Empty,
        FullPathComponents = [],
        RelativePathComponents = [],
        Type = EntryType.Dir,
        Size = 0,
        Properties = new Dictionary<string, string>()
    };

    /// <summary>
    /// Is same media and partition examines if volumes are mounted from same media and partition.
    /// </summary>
    public static bool IsSameMediaAndPartition(IVolume volume, IVolume other) =>
        other != null &&
        (ReferenceEquals(volume, other) ||
         (volume.Media != null && other.Media != null &&
          volume.Media.Equals(other.Media) &&
          volume.PartitionTableType == other.PartitionTableType &&
          volume.PartitionNumber == other.PartitionNumber));

    public static Result CreateReadOnlyResult(IVolume volume, string operation) =>
        new(CreateReadOnlyError(volume, operation));

    public static Result<T> CreateReadOnlyResult<T>(IVolume volume, string operation) =>
        new(CreateReadOnlyError(volume, operation));

    private static VolumeReadOnlyError CreateReadOnlyError(IVolume volume, string operation) =>
        new($"{operation} is not supported for read-only {volume.GetType().Name}");

    public static bool IsDir(this Entry entry) => entry.Type is EntryType.Dir or EntryType.LinkDir;

    public static bool IsFile(this Entry entry) => entry.Type is EntryType.File or EntryType.LinkFile;

    public static string Join(this IMediaPath mediaPath, IEnumerable<string> pathComponents) =>
        mediaPath.Join(pathComponents is string[] array ? array : [.. pathComponents]);

    public static StringComparer NameComparer => StringComparer.OrdinalIgnoreCase;
}
