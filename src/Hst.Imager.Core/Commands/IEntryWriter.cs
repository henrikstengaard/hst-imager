namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hst.Imager.Core.UaeMetadatas;
using Models.FileSystems;

public interface IEntryWriter : IDisposable
{
    string MediaPath { get; }
    string FileSystemPath { get; }

    /// <summary>
    /// Create directory for entry
    /// </summary>
    /// <param name="entry">Entry to create directory dir</param>
    /// <param name="entryPathComponents">Entry path components to entry</param>
    /// <returns></returns>
    Task CreateDirectory(Entry entry, string[] entryPathComponents, bool skipAttributes);
    
    /// <summary>
    /// Write entry to stream 
    /// </summary>
    /// <param name="entry">Entry to write</param>
    /// <param name="entryPathComponents">Entry path components to entry</param>
    /// <param name="stream">Stream to write entry to</param>
    /// <returns></returns>
    Task WriteEntry(Entry entry, string[] entryPathComponents, Stream stream, bool skipAttributes);
    
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

    IEntryIterator CreateEntryIterator(string rootPath, bool recursive);
    
    UaeMetadata UaeMetadata { get; set; }
}