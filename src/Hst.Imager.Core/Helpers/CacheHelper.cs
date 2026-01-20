using System.IO;
using Hst.Core.IO;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Helpers;

public static class CacheHelper
{
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
            
        return new LayeredStream(baseStream, layerStream, new LayeredStreamOptions
        {
            FlushLayerOnDispose = writeable,
            BlockSize = blockSize
        });
    }
}