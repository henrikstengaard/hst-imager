using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Commands;

using Hst.Imager.Core.UaeMetadatas;
using System;
using System.IO;
using System.Threading.Tasks;
using Entry = Models.FileSystems.Entry;

public interface IEntryIterator : IDisposable
{
    Media Media { get; }
    string RootPath { get; }
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