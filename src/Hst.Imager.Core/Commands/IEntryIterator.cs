using Hst.Core;
using Hst.Imager.Core.FileSystems;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.PathComponents;

namespace Hst.Imager.Core.Commands;

using UaeMetadatas;
using System;
using System.IO;
using System.Threading.Tasks;
using Entry = Models.FileSystems.Entry;

public interface IEntryIterator : IDisposable
{
    IMediaPath MediaPath { get; }
    Task<Result> Initialize();
    string[] PathComponents { get; }
    string[] DirPathComponents { get; }
    
    /// <summary>
    /// Default attributes mode for iterator.
    /// </summary>
    AttributesMode AttributesMode { get; }
    
    /// <summary>
    /// Media mounted.
    /// </summary>
    Media Media { get; }

    /// <summary>
    /// Partition table type mounted.
    /// </summary>
    PartitionTableType PartitionTableType { get; }

    /// <summary>
    /// Partition number mounted.
    /// </summary>
    int PartitionNumber { get; }

    Entry Current { get; }
    Task<bool> Next();
    bool HasMoreEntries { get; }
    bool IsSingleFileEntryNext { get; }
    Task<Stream> OpenEntry(Entry entry);
    Task<Result> DeleteEntry(string[] fullPathComponents);

    /// <summary>
    /// Set attributes of entry by applying attributes change to current attributes of entry.
    /// </summary>
    /// <param name="fullPathComponents">Full path components of entry.</param>
    /// <param name="attributesChange">Attributes change to apply.</param>
    /// <returns>Attributes set for entry.</returns>
    Task<Result<int>> SetAttributes(string[] fullPathComponents, AttributesChange attributesChange);
    string[] GetPathComponents(string path);
    bool UsesPattern { get; }
    Task Flush();
    bool SupportsUaeMetadata { get; }
    UaeMetadata UaeMetadata { get; set; }
}