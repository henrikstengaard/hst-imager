using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Hst.Imager.Core.Models.FileSystems;
using Hst.Imager.Core.UaeMetadatas;
using Hst.Imager.Core.Volumes;
using Xunit;

namespace Hst.Imager.Core.Tests.VolumeTests;

public enum VolumeType
{
    Amiga,
    DiscUtils,
    LocalDirectory
}

/// <summary>
/// Volume test context with test command helper, volume factory and paths for a volume type.
/// </summary>
public sealed class VolumeTestContext : IDisposable
{
    private readonly string _mediaPath;
    private readonly UaeMetadata _uaeMetadata;

    private VolumeTestContext(VolumeType volumeType, string mediaPath, string volumePath,
        UaeMetadata uaeMetadata)
    {
        VolumeType = volumeType;
        _mediaPath = mediaPath;
        _uaeMetadata = uaeMetadata;
        VolumePath = volumePath;
        TestCommandHelper = new TestCommandHelper();
        AppCache = new TestAppCache();
        UaeMetadataHelper = new UaeMetadataHelper(AppCache);
        Factory = new VolumeFactory(TestCommandHelper, [], UaeMetadataHelper, uaeMetadata);
    }

    public VolumeType VolumeType { get; }
    public string VolumePath { get; }
    public TestCommandHelper TestCommandHelper { get; }
    public TestAppCache AppCache { get; }
    public UaeMetadataHelper UaeMetadataHelper { get; }
    public VolumeFactory Factory { get; private set; }

    public static async Task<VolumeTestContext> Create(VolumeType volumeType,
        UaeMetadata uaeMetadata = UaeMetadata.None)
    {
        var name = Guid.NewGuid().ToString();
        VolumeTestContext context;
        switch (volumeType)
        {
            case VolumeType.Amiga:
                var amigaMediaPath = $"{name}.vhd";
                context = new VolumeTestContext(volumeType, amigaMediaPath,
                    Path.Combine(amigaMediaPath, "rdb", "dh0"), uaeMetadata);
                await TestHelper.CreatePfs3FormattedDisk(context.TestCommandHelper, amigaMediaPath, 100.MB());
                break;
            case VolumeType.DiscUtils:
                var discUtilsMediaPath = $"{name}.vhd";
                context = new VolumeTestContext(volumeType, discUtilsMediaPath,
                    Path.Combine(discUtilsMediaPath, "mbr", "1"), uaeMetadata);
                await TestHelper.CreateMbrFatFormattedDisk(context.TestCommandHelper, discUtilsMediaPath, 100.MB());
                break;
            case VolumeType.LocalDirectory:
                Directory.CreateDirectory(name);
                context = new VolumeTestContext(volumeType, name, Path.GetFullPath(name), uaeMetadata);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(volumeType), volumeType, null);
        }

        return context;
    }

    /// <summary>
    /// Open volume for path relative to volume path.
    /// </summary>
    public async Task<VolumePath> Open(VolumeAccess access = VolumeAccess.Write, params string[] pathComponents)
    {
        var path = pathComponents.Length == 0
            ? VolumePath
            : Path.Combine(VolumePath, Path.Combine(pathComponents));
        var result = await Factory.Open(path, access);
        Assert.True(result.IsSuccess, result.IsFaulted ? result.Error.ToString() : null);
        return result.Value;
    }

    /// <summary>
    /// Reopen volume factory flushing and disposing volumes opened.
    /// </summary>
    public void Reopen()
    {
        Factory.Dispose();
        Factory = new VolumeFactory(TestCommandHelper, [], UaeMetadataHelper, _uaeMetadata);
    }

    /// <summary>
    /// Create directories and files in volume:
    /// - dir1
    /// - dir1/dir2
    /// - dir1/dir2/file3.txt
    /// - dir1/file2.txt
    /// - file1.txt
    /// - file2.bin
    /// </summary>
    public async Task CreateDirectoriesAndFiles()
    {
        var (volume, root) = await Open();
        await AssertSuccess(volume.CreateDirectory([..root, "dir1"]));
        await AssertSuccess(volume.CreateDirectory([..root, "dir1", "dir2"]));
        await WriteFile(volume, [..root, "file1.txt"], "file1");
        await WriteFile(volume, [..root, "file2.bin"], "file2");
        await WriteFile(volume, [..root, "dir1", "file2.txt"], "dir1 file2");
        await WriteFile(volume, [..root, "dir1", "dir2", "file3.txt"], "dir2 file3");
        await volume.Flush();
    }

    public static async Task WriteFile(IVolume volume, string[] pathComponents, string text)
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(text));
        await AssertSuccess(volume.WriteFile(pathComponents, stream, false));
    }

    public static async Task<string> ReadFile(IVolume volume, string[] pathComponents)
    {
        var openResult = await volume.OpenFile(pathComponents);
        Assert.True(openResult.IsSuccess, openResult.IsFaulted ? openResult.Error.ToString() : null);
        await using var stream = openResult.Value;
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return Encoding.ASCII.GetString(memoryStream.ToArray());
    }

    public static async Task AssertSuccess(Task<Hst.Core.Result> resultTask)
    {
        var result = await resultTask;
        Assert.True(result.IsSuccess, result.IsFaulted ? result.Error.ToString() : null);
    }

    public static async Task<Entry[]> ReadAll(VolumeEntryWalker walker)
    {
        var entries = new System.Collections.Generic.List<Entry>();
        while (await walker.Next())
        {
            entries.Add(walker.Current);
        }

        return entries.ToArray();
    }

    public static string[] RelativePaths(Entry[] entries) =>
        entries.Select(x => string.Join("/", x.RelativePathComponents)).ToArray();

    public void Dispose()
    {
        Factory.Dispose();
        TestCommandHelper.Dispose();
        AppCache.Dispose();
        TestHelper.DeletePaths(_mediaPath);
    }
}
