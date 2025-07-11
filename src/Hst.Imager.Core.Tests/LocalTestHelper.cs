using System.IO;
using System.Threading.Tasks;

namespace Hst.Imager.Core.Tests;

public static class LocalTestHelper
{
    public static async Task CreateDirectoriesAndFiles(string mediaPath)
    {
        var dir1Path = Path.Combine(mediaPath, "dir1");
        var dir2Path = Path.Combine(mediaPath, "dir2");
        var dir3Path = Path.Combine(dir1Path, "dir3");

        if (!Directory.Exists(dir2Path))
        {
            Directory.CreateDirectory(dir2Path);
        }

        if (!Directory.Exists(dir3Path))
        {
            Directory.CreateDirectory(dir3Path);
        }
        
        var file1TxtPath = Path.Combine(dir1Path, "file1.txt");

        await File.WriteAllTextAsync(file1TxtPath, string.Empty);
    }
}