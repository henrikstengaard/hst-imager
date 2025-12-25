using Hst.Core;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UaeMetadatas;
using Models.FileSystems;

public interface IEntryWriter : IDisposable
{
    Media Media { get; }
    string MediaPath { get; }
    string FileSystemPath { get; }

    /// <summary>
    /// Initialize the entry writer verifying the root path components exist.
    /// Root path components must exist except the last one.
    /// The last root path component can be a directory or a file that either exists or doesn't exist.
    /// - If the last root path component exists and is a directory, it will be used as the root directory for creating directories and files.
    /// - If the last root path component exists and is a file, then only a file entry can be created and will overwrite the file.
    /// - If the last root path component does not exist, then only a file entry can be created as a new file.
    /// </summary>
    /// <returns>Last root path component, if it doesn't exist.</returns>
    Task<Result> Initialize();
    
    /// <summary>
    /// Create directory for entry
    /// </summary>
    /// <param name="entry">Entry to create directory dir</param>
    /// <param name="entryPathComponents">Entry path components to entry</param>
    /// <returns></returns>
    Task<Result> CreateDirectory(Entry entry, string[] entryPathComponents, bool skipAttributes, bool singleEntry);

    /// <summary>
    /// Create file for entry.
    /// </summary>
    /// <param name="entry">Entry to write</param>
    /// <param name="entryPathComponents">Entry path components to entry</param>
    /// <param name="stream">Stream to write entry to</param>
    /// <param name="skipAttributes"></param>
    /// <param name="singleFile"></param>
    /// <returns></returns>
    Task<Result> CreateFile(Entry entry, string[] entryPathComponents, Stream stream, bool skipAttributes, bool singleFile);
    
    /// <summary>
    /// Flush changes to stream
    /// </summary>
    /// <returns></returns>
    Task Flush();
    
    /// <summary>
    /// Get debug logs from entry writer
    /// </summary>
    /// <returns></returns>
    IEnumerable<string> GetDebugLogs();
    
    /// <summary>
    /// Get logs from entry writer
    /// </summary>
    /// <returns></returns>
    IEnumerable<string> GetLogs();

    IEntryIterator CreateEntryIterator(string[] rootPathComponents, bool recursive);
    bool ArePathComponentsSelfCopy(IEntryIterator entryIterator);
    bool ArePathComponentsCyclic(IEntryIterator entryIterator);
    
    bool SupportsUaeMetadata { get; }

    UaeMetadata UaeMetadata { get; set; }
}