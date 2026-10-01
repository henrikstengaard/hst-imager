using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;

namespace Hst.Imager.AvaloniaApp.Services;

public interface IMediaService
{
    Task<IEnumerable<MediaInfo>> ListMediaAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Media is blank, if first sectors used by master boot record, guid partition table and rigid disk block
    /// only contain zeroes.
    /// </summary>
    Task<bool> IsBlankAsync(string path, bool byteswap = false, CancellationToken cancellationToken = default);
    Task<MediaInfo?> GetMediaInfoAsync(string path, bool byteswap = false, bool allowNonExisting = false, CancellationToken cancellationToken = default);
}
