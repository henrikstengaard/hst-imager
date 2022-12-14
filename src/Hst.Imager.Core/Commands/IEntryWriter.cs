namespace Hst.Imager.Core.Commands;

using System.IO;
using System.Threading.Tasks;
using Models.FileSystems;

public interface IEntryWriter
{
    Task Write(Entry entry, Stream stream);
}