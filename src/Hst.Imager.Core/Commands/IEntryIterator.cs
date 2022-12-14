namespace Hst.Imager.Core.Commands;

using System;
using System.Threading.Tasks;
using Entry = Models.FileSystems.Entry;

public interface IEntryIterator : IDisposable
{
    Entry Current { get; }
    Task<bool> Next();
}