using System;
using System.IO;
using System.Threading.Tasks;
using DiscUtils.Fat;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.Tests.EntryWriterTests;

public static class EntryWriterTestHelper
{
    public static string CreateMediaPath(EntryWriterType entryWriterType)
    {
        switch (entryWriterType)
        {
            case EntryWriterType.AmigaVolumeEntryWriter:
            case EntryWriterType.FileSystemEntryWriter:
                return string.Concat(Guid.NewGuid(), ".vhd");
            case EntryWriterType.DirectoryEntryWriter:
                return Guid.NewGuid().ToString();
            default:
                throw new ArgumentOutOfRangeException(nameof(entryWriterType), entryWriterType,
                    "Entry writer type not supported");
        }
    }
    
    public static async Task CreateMedia(EntryWriterType entryWriterType,
        TestCommandHelper testCommandHelper,
        string path)
    {
        switch (entryWriterType)
        {
            case EntryWriterType.AmigaVolumeEntryWriter:
                testCommandHelper.AddTestMedia(path, 0);
                await TestHelper.CreatePfs3FormattedDisk(testCommandHelper, path, 100.MB());
                break;
            case EntryWriterType.FileSystemEntryWriter:
                testCommandHelper.AddTestMedia(path, 0);
                await TestHelper.CreateMbrFatFormattedDisk(testCommandHelper, path, 100.MB());
                break;
            case EntryWriterType.DirectoryEntryWriter:
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entryWriterType), entryWriterType,
                    "Entry writer type not supported");
        }
    }

    public static async Task<IEntryWriter> CreateEntryWriter(EntryWriterType entryWriterType,
        TestCommandHelper testCommandHelper,
        string path, string[] initializePathComponents, bool createDestDirectory)
    {
        return entryWriterType switch
        {
            EntryWriterType.AmigaVolumeEntryWriter => await CreateAmigaVolumeEntryWriter(
                testCommandHelper, path, initializePathComponents, createDestDirectory),
            EntryWriterType.FileSystemEntryWriter => await CreateFileSystemEntryWriter(
                testCommandHelper, path, initializePathComponents, createDestDirectory),
            EntryWriterType.DirectoryEntryWriter => CreateDirectoryEntryWriter(
                testCommandHelper, path, initializePathComponents, createDestDirectory),
            _ => throw new ArgumentOutOfRangeException(nameof(entryWriterType), entryWriterType,
                "Entry writer type not supported")
        };
    }

    public static async Task<AmigaVolumeEntryWriter> CreateAmigaVolumeEntryWriter(TestCommandHelper testCommandHelper,
        string diskPath, string[] initializePathComponents, bool createDestDirectory)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(diskPath);
        if (mediaResult.IsFaulted)
        {
            throw new IOException(mediaResult.Error.ToString());
        }

        var media = mediaResult.Value;
        var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;

        await using var pfs3Volume = await TestHelper.MountPfs3Volume(stream);

        return new AmigaVolumeEntryWriter(media, PartitionTableType.RigidDiskBlock, 0, 
            string.Empty, initializePathComponents, false, pfs3Volume, createDestDirectory, false);
    }
    
    public static async Task<IEntryWriter> CreateFileSystemEntryWriter(TestCommandHelper testCommandHelper, 
        string diskPath, string[] initializePathComponents, bool createDestDirectory)
    {
        var mediaResult = await testCommandHelper.GetWritableFileMedia(diskPath);
        if (mediaResult.IsFaulted)
        {
            throw new IOException(mediaResult.Error.ToString());
        }

        var media = mediaResult.Value;
        var disk = media is DiskMedia diskMedia
            ? diskMedia.Disk
            : new DiscUtils.Raw.Disk(media.Stream, Ownership.None);

        var biosPartitionTable = new BiosPartitionTable(disk);
        var fatFileSystem = new FatFileSystem(biosPartitionTable.Partitions[0].Open());

        return new FileSystemEntryWriter(media, PartitionTableType.MasterBootRecord, 0, fatFileSystem,
            initializePathComponents, false, createDestDirectory, false);
    }

    public static IEntryWriter CreateDirectoryEntryWriter(TestCommandHelper testCommandHelper,
        string path, string[] initializePathComponents, bool createDestDirectory)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        
        return new DirectoryEntryWriter(Path.Combine(path, Path.Combine(initializePathComponents)), false,
            createDestDirectory, false);
    }

    public static async Task CreateDirectory(EntryWriterType entryWriterType, TestCommandHelper testCommandHelper,
        string path, string[] dirPathComponents)
    {
        switch (entryWriterType)
        {
            case EntryWriterType.AmigaVolumeEntryWriter:
                await TestHelper.CreateRdbPfs3Directory(testCommandHelper, path, dirPathComponents);
                break;
            case EntryWriterType.FileSystemEntryWriter:
                await TestHelper.CreateMbrFatDirectory(testCommandHelper, path, dirPathComponents);
                break;
            case EntryWriterType.DirectoryEntryWriter:
                TestHelper.CreateLocalDirectory(path, dirPathComponents);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entryWriterType), entryWriterType,
                    "Entry writer type not supported");
        }
    }
}