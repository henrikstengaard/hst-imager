namespace Hst.Imager.Core.Commands;

using Hst.Imager.Core.UaeMetadatas;
using System;
using System.IO;
using System.Threading.Tasks;
using Entry = Models.FileSystems.Entry;

public interface IEntryIterator : IDisposable
{
    string RootPath { get; }
    Entry Current { get; }
    Task<bool> Next();
    Task<Stream> OpenEntry(Entry entry);
    string[] GetPathComponents(string path);
    bool UsesPattern { get; }
    Task Flush();
    UaeMetadata UaeMetadata { get; set; }
}