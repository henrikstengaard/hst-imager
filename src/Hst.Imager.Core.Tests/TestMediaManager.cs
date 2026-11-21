namespace Hst.Imager.Core.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils;
using DiscUtils.Streams;
using Hst.Core;
using Hst.Imager.Core.Helpers;
using Hst.Imager.Core.MagicBytes;
using Hst.Imager.Core.Medias;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Media manager opening test medias in memory instead of disk access. Paths not added as test medias
/// are opened from disk. Physical drives are never accessed.
/// </summary>
public class TestMediaManager : MediaManager
{
    private readonly IList<TestMedia> testMedias;

    public static readonly string PhysicalDrivePath = Hst.Core.OperatingSystem.IsWindows()
        ? @"\\.\PhysicalDrive0"
        : "/dev/disk0";

    public TestMediaManager(IList<TestMedia> testMedias, IMediaPathResolver pathResolver)
        : base(NullLogger.Instance, pathResolver, true, false, false, CacheType.Disk)
    {
        this.testMedias = testMedias;
    }

    private TestMedia GetTestMedia(string path) =>
        testMedias.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

    public override async Task<DataType> DetectDataType(string path)
    {
        var testMedia = GetTestMedia(path);

        if (testMedia == null)
        {
            return await base.DetectDataType(path);
        }

        return await DetectDataType(testMedia.Stream);
    }

    public override bool IsVhdValid(string path)
    {
        var testMedia = GetTestMedia(path);

        if (testMedia == null)
        {
            return base.IsVhdValid(path);
        }

        if (testMedia.Stream.Length == 0)
        {
            return false;
        }

        VirtualDisk disk;

        try
        {
            disk = new DiscUtils.Vhd.Disk(testMedia.Stream, Ownership.None);
        }
        catch (Exception)
        {
            return false;
        }

        disk.Dispose();
        return true;
    }

    /// <summary>
    /// Overrides get physical drive media to prevent access to physical drives while running unit tests.
    /// </summary>
    /// <param name="physicalDrives">List of physical drives.</param>
    /// <param name="path">Path to physical drive.</param>
    /// <param name="modifiers">Modifiers to use.</param>
    /// <param name="writeable">Get writeable media.</param>
    /// <returns>Media.</returns>
    /// <exception cref="ArgumentNullException">Null exception thrown, if path is null.</exception>
    public override Task<Result<Media>> GetPhysicalDriveMedia(IEnumerable<IPhysicalDrive> physicalDrives, string path,
        ModifierEnum? modifiers = null, bool writeable = false)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path != PhysicalDrivePath)
        {
            return Task.FromResult(new Result<Media>((Media)null));
        }

        var testMedia = GetTestMedia(path);

        if (testMedia == null)
        {
            return Task.FromResult(new Result<Media>((Media)null));
        }

        var stream = testMedia.Stream;
        if (!testMedia.Path.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new Result<Media>(new Media(testMedia.Path, testMedia.Name,
                Media.MediaType.Raw, false, stream, false)));
        }

        var disk = new DiscUtils.Vhd.Disk(stream, Ownership.Dispose);
        return Task.FromResult(new Result<Media>(new DiskMedia(testMedia.Path, testMedia.Name, Media.MediaType.Vhd,
            false, disk, false, stream)));
    }

    public override async Task<Result<Media>> GetReadableFileMedia(string path, ModifierEnum? modifiers = null)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        var testMedia = GetTestMedia(path);
        if (testMedia == null)
        {
            return await base.GetReadableFileMedia(path, modifiers);
        }

        var byteSwap = false;
        var modifierResult = PathResolver.ResolveModifiers(path);
        if (modifierResult.HasModifiers)
        {
            byteSwap = modifierResult.Modifiers.HasFlag(ModifierEnum.ByteSwap);
        }

        var stream = testMedia.Stream;
        if (!testMedia.Path.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase))
        {
            return new Result<Media>(new Media(testMedia.Path, testMedia.Name,
                Media.MediaType.Raw, false, stream, byteSwap));
        }

        stream.Position = 0;
        var vhdDisk = new DiscUtils.Vhd.Disk(stream, Ownership.None);
        Stream baseStream = byteSwap
            ? new SectorStream(vhdDisk.Content, byteSwap: true, leaveOpen: false)
            : vhdDisk.Content;
        var disk = new DiskMedia(testMedia.Path, testMedia.Name, Media.MediaType.Vhd,
            false, vhdDisk, byteSwap, baseStream);
        return new Result<Media>(disk);
    }

    public override Task<Result<Media>> GetWritableFileMedia(string path, ModifierEnum? modifiers = null,
        long? size = null, bool create = false)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        var testMedia = GetTestMedia(path);
        if (testMedia == null)
        {
            return base.GetWritableFileMedia(path, modifiers, size, create);
        }

        if (!testMedia.Path.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase))
        {
            if (create && size.HasValue)
            {
                testMedia.Stream.SetLength(size.Value);
            }

            return Task.FromResult(new Result<Media>(new Media(testMedia.Path, testMedia.Name,
                Media.MediaType.Raw, false, testMedia.Stream, false)));
        }

        if (size.HasValue)
        {
            size = MediaHelper.GetVhdSize(size.Value);
        }

        if (create && size == null)
        {
            return Task.FromResult(new Result<Media>(new Error("Vhd requires size")));
        }

        var byteSwap = false;
        var modifierResult = PathResolver.ResolveModifiers(path);
        if (modifierResult.HasModifiers)
        {
            byteSwap = modifierResult.Modifiers.HasFlag(ModifierEnum.ByteSwap);
        }

        var stream = testMedia.Stream;
        var isEmptyStream = stream.Length == 0;
        var vhdDisk = create || isEmptyStream
            ? DiscUtils.Vhd.Disk.InitializeDynamic(stream, Ownership.None, size ?? 0)
            : new DiscUtils.Vhd.Disk(stream, Ownership.None);

        Stream baseStream = byteSwap
            ? new SectorStream(vhdDisk.Content, byteSwap: true, leaveOpen: false)
            : vhdDisk.Content;
        var disk = new DiskMedia(testMedia.Path, testMedia.Name,
            Media.MediaType.Vhd, false, vhdDisk, false, baseStream);
        return Task.FromResult(new Result<Media>(disk));
    }

    public override Task<Result<Media>> CreateSparseFileMedia(string path, long size)
    {
        return GetTestMedia(path) != null
            ? GetWritableFileMedia(path, size: size, create: true)
            : base.CreateSparseFileMedia(path, size);
    }

    public override Stream CreateWriteableStream(string path, bool create, long size)
    {
        var testMedia = GetTestMedia(path);
        return testMedia != null
            ? testMedia.Stream
            : base.CreateWriteableStream(path, create, size);
    }

    public override Task RescanPhysicalDrives()
    {
        return Task.CompletedTask;
    }
}
