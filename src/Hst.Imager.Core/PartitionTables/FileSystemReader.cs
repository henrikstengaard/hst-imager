using System;
using System.Threading.Tasks;
using Hst.Imager.Core.FileSystems.Ext;

namespace Hst.Imager.Core.PartitionTables;

public static class FileSystemReader
{
    public static async Task<string> ReadFileSystem(DiscUtils.Partitions.PartitionInfo partitionInfo)
    {
        await using var stream = partitionInfo.Open();
        
        try
        {
            var extFileSystemInfo = await ExtFileSystemReader.Read(stream);
            return extFileSystemInfo.Version.ToString();
        }
        catch (Exception e)
        {
            // ignored, if errors occur
        }

        return string.Empty;
    }
}