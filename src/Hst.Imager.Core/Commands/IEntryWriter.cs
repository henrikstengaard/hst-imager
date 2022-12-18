namespace Hst.Imager.Core.Commands;

using System;
using System.IO;
using System.Threading.Tasks;
using Models.FileSystems;

public interface IEntryWriter : IDisposable
{
    Task CreateDirectory(Entry entry);
    Task WriteEntry(Entry entry, Stream stream);
}