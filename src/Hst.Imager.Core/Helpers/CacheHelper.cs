using System.IO;
using Hst.Core.IO;

namespace Hst.Imager.Core.Helpers;

public static class CacheHelper
{
    public static Stream AddLayeredCache(string path, Stream baseStream, bool writable, int blockSize = 1024 * 1024)
    {
        var layerPath = PathHelper.GetLayerPath(path);

        // delete layer path, if file exists 
        if (File.Exists(layerPath))
        {
            File.Delete(layerPath);
        }
            
        var layerStream = File.Open(layerPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            
        return new LayeredStream(baseStream, layerStream, new LayeredStreamOptions
        {
            FlushLayerOnDispose = writable,
            BlockSize = blockSize
        });
    }
}