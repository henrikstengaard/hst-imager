using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;

namespace Hst.Imager.AvaloniaApp.Services;

public interface IMediaService
{
    Task<IEnumerable<MediaInfo>> ListMediaAsync(CancellationToken cancellationToken = default);
    Task<MediaInfo?> GetMediaInfoAsync(string path, bool byteswap = false, bool allowNonExisting = false, CancellationToken cancellationToken = default);
}
