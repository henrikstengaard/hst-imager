namespace Hst.Imager.Core.Commands;

using System;
using System.IO;
using System.Threading.Tasks;
using Entry = Models.FileSystems.Entry;

public interface IEntryIterator : IDisposable
{
    string RootPath { get; }
    Entry Current { get; }
    Task<bool> Next();
    Task<Stream> OpenEntry(string path);
}