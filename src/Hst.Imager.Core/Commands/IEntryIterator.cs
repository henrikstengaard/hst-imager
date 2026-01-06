using Hst.Core;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Commands;

using UaeMetadatas;
using System;
using System.IO;
using System.Threading.Tasks;
using Entry = Models.FileSystems.Entry;

public interface IEntryIterator : IDisposable
{
    Task<Result> Initialize();
    string[] PathComponents { get; }
    string[] DirPathComponents { get; }
    
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
    string[] GetPathComponents(string path);
    bool UsesPattern { get; }
    Task Flush();
    bool SupportsUaeMetadata { get; }
    UaeMetadata UaeMetadata { get; set; }
}