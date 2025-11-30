using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hst.Amiga;
using Hst.Amiga.FileSystems;
using Hst.Amiga.FileSystems.FastFileSystem;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Tests;

public static class AdfTestHelper
{
    public static async Task CreateFormattedAdfDisk(TestCommandHelper testCommandHelper, string mediaPath)
    {
        testCommandHelper.AddTestMedia(mediaPath, FloppyDiskConstants.DoubleDensity.Size);

        var mediaResult = await testCommandHelper.GetWritableFileMedia(mediaPath);
        using var media = mediaResult.Value;
        var stream = media.Stream;
        
        await FastFileSystemFormatter.Format(stream, FloppyDiskConstants.DoubleDensity.LowCyl,
            FloppyDiskConstants.DoubleDensity.HighCyl, FloppyDiskConstants.DoubleDensity.ReservedBlocks,
            FloppyDiskConstants.DoubleDensity.Heads, FloppyDiskConstants.DoubleDensity.Sectors,
            FloppyDiskConstants.BlockSize, FloppyDiskConstants.BlockSize, TestHelper.Dos3DosType, "Amiga");
    }

    public static async Task<(Media, IFileSystemVolume)> MountFileSystemVolume(TestCommandHelper testCommandHelper,
        string mediaPath, bool writable = false)
    {
        var mediaResult = writable
            ? await testCommandHelper.GetWritableFileMedia(mediaPath)
            : await testCommandHelper.GetReadableFileMedia(mediaPath);
        if (mediaResult.IsFaulted)
        {
            throw new IOException(mediaResult.Error.ToString());
        }
            
        var media = mediaResult.Value;
        
        return (media, await FastFileSystemVolume.MountAdf(media.Stream));
    }

    /// <summary>
    /// Create
    /// - dir1
    ///   - dir3
    ///   - file1.txt
    /// - dir2
    /// 
    /// </summary>
    /// <param name="testCommandHelper"></param>
    /// <param name="path"></param>
    /// <exception cref="IOException"></exception>
    public static async Task CreateDirectoriesAndFiles(TestCommandHelper testCommandHelper, string path)
    {
        var (media, fileSystemVolume) = await MountFileSystemVolume(testCommandHelper, path, true);
        
        await fileSystemVolume.CreateDirectory("dir1");
        await fileSystemVolume.CreateDirectory("dir2");
        await fileSystemVolume.ChangeDirectory("dir1");
        await fileSystemVolume.CreateDirectory("dir3");
        await fileSystemVolume.CreateFile("file1.txt", true, true);
        
        media.Dispose();
    }
    
    public static async Task CreateDirectory(
        TestCommandHelper testCommandHelper, string mediaPath, string[] dirPathComponents)
    {
        var (media, fileSystemVolume) = await MountFileSystemVolume(testCommandHelper, mediaPath, true);

        foreach (var dirPathComponent in dirPathComponents)
        {
            await fileSystemVolume.CreateDirectory(dirPathComponent);
            await fileSystemVolume.ChangeDirectory(dirPathComponent);
        }
        
        media.Dispose();
    }

    public static async Task CreateFile(
        TestCommandHelper testCommandHelper, string mediaPath, string[] dirPathComponents)
    {
        var (media, fileSystemVolume) = await MountFileSystemVolume(testCommandHelper, mediaPath, true);

        foreach (var dirPathComponent in dirPathComponents.Take(dirPathComponents.Length - 1))
        {
            var entries = (await fileSystemVolume.ListEntries()).ToList();

            if (entries.FirstOrDefault(x => x.Name == dirPathComponent) == null)
            {
                await fileSystemVolume.CreateDirectory(dirPathComponent);
            }
            
            await fileSystemVolume.ChangeDirectory(dirPathComponent);
        }
        
        await fileSystemVolume.CreateFile(dirPathComponents[^1], true, true);
        
        media.Dispose();
    }
    
    public static async Task<IEnumerable<Amiga.FileSystems.Entry>> GetEntriesFromFileSystemVolume(
        TestCommandHelper testCommandHelper, string mediaPath, string[] dirPathComponents, bool writable = false)
    {
        var (media, fileSystemVolume) = await MountFileSystemVolume(testCommandHelper, mediaPath, writable);

        foreach (var dirPathComponent in dirPathComponents)
        {
            await fileSystemVolume.ChangeDirectory(dirPathComponent);
        }

        var entries = await fileSystemVolume.ListEntries();
        
        media.Dispose();

        return entries;
    }
}