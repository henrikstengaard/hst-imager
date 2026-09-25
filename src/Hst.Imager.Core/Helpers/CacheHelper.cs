using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Hst.Core.IO;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Helpers;

public static class CacheHelper
{
    /// <summary>
    /// Options for layered caches created, used to discard cached writes. Weak references are used, so disposed
    /// layered streams are not kept alive.
    /// </summary>
    private static readonly ConditionalWeakTable<LayeredStream, LayeredStreamOptions> LayeredCaches = new();

    public static Stream AddLayeredCache(string path, Stream baseStream, bool writeable, int blockSize = 1024 * 1024,
        CacheType cacheType = CacheType.Disk)
    {
        var layerPath = PathHelper.GetLayerPath(path);

        if (string.IsNullOrEmpty(layerPath))
        {
            throw new IOException("Layer path is null or empty");
        }

        // delete layer path, if file exists
        if (File.Exists(layerPath))
        {
            File.Delete(layerPath);
        }

        Stream layerStream = cacheType == CacheType.Disk
            ? File.Open(layerPath, FileMode.OpenOrCreate, FileAccess.ReadWrite)
            : new MemoryStream();

        var options = new LayeredStreamOptions
        {
            FlushLayerOnDispose = writeable,
            BlockSize = blockSize
        };
        var layeredStream = new LayeredStream(baseStream, layerStream, options);

        CommandLogger.Instance.AddLoggingOf(layeredStream);
        LayeredCaches.AddOrUpdate(layeredStream, options);

        return layeredStream;
    }

    /// <summary>
    /// Discard cached writes for layered caches not yet disposed, so cached writes are not flushed to base streams
    /// when disposed. Used when cancelling, so data cached is not written to destinations.
    /// </summary>
    public static void DiscardCachedWrites()
    {
        foreach (var (layeredStream, options) in LayeredCaches.ToList())
        {
            if (!layeredStream.IsDisposed)
            {
                options.FlushLayerOnDispose = false;
            }
        }
    }
}
