using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Core;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Commands.RecoveryCommands;
using Hst.Imager.Core.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hst.Imager.Core.Tests.CommandTests.RecoveryCommandTests;

public abstract class RecoveryTestBase : FsCommandTestBase, IDisposable
{
    protected readonly string LogPath = $"recovery_{Guid.NewGuid()}.log";
    private readonly List<string> _paths = [];

    protected string AddCleanupPath(string path)
    {
        _paths.Add(path);
        return path;
    }

    public void Dispose()
    {
        DeletePaths(_paths.Concat([LogPath]).ToArray());
        GC.SuppressFinalize(this);
    }

    protected static async Task<(Result Result, RecoveryReport Report)> Execute(RecoveryCommandBase command)
    {
        RecoveryReport report = null;
        command.RecoveryReportRead += (_, args) => report = args.Report;
        var result = await command.Execute(CancellationToken.None);
        return (result, report);
    }

    protected async Task<(Result Result, RecoveryReport Report)> ScanPartitionTable(
        TestCommandHelper testCommandHelper, string path, PartitionTableType? type = null) =>
        await Execute(new PartitionTableScanCommand(new NullLogger<PartitionTableScanCommand>(), testCommandHelper,
            [], path, LogPath, type));

    protected async Task<(Result Result, RecoveryReport Report)> RepairPartitionTable(
        TestCommandHelper testCommandHelper, string path, PartitionTableType? type = null) =>
        await Execute(new PartitionTableRepairCommand(new NullLogger<PartitionTableRepairCommand>(),
            testCommandHelper, [], path, LogPath, type));

    protected async Task<(Result Result, RecoveryReport Report)> ScanFileSystem(
        TestCommandHelper testCommandHelper, string path) =>
        await Execute(new FsScanCommand(new NullLogger<FsScanCommand>(), testCommandHelper, [], path, LogPath));

    protected async Task<(Result Result, RecoveryReport Report)> RepairFileSystem(
        TestCommandHelper testCommandHelper, string path) =>
        await Execute(new FsRepairCommand(new NullLogger<FsRepairCommand>(), testCommandHelper, [], path, LogPath));

    protected static async Task<RigidDiskBlock> ReadRigidDiskBlock(TestCommandHelper testCommandHelper,
        string path)
    {
        var mediaResult = await testCommandHelper.GetReadableMedia([], path);
        using var media = mediaResult.Value;
        return await MediaHelper.ReadRigidDiskBlockFromMedia(media);
    }

    /// <summary>
    /// Get offset of first partition in rigid disk block.
    /// </summary>
    protected static async Task<(long Offset, PartitionBlock PartitionBlock)> GetFirstPartitionOffset(
        TestCommandHelper testCommandHelper, string path)
    {
        var rigidDiskBlock = await ReadRigidDiskBlock(testCommandHelper, path);
        var partitionBlock = rigidDiskBlock.PartitionBlocks.First();
        var offset = (long)partitionBlock.LowCyl * partitionBlock.Surfaces * partitionBlock.BlocksPerTrack * 512;
        return (offset, partitionBlock);
    }

    /// <summary>
    /// Find offset of fast file system root block in first partition by searching for block with type T_HEADER
    /// and secondary type ST_ROOT.
    /// </summary>
    protected static async Task<long> FindFastFileSystemRootBlockOffset(TestCommandHelper testCommandHelper,
        string path)
    {
        var (partitionOffset, partitionBlock) = await GetFirstPartitionOffset(testCommandHelper, path);
        var blockSize = (int)partitionBlock.FileSystemBlockSize;
        var blocks = (long)(partitionBlock.HighCyl - partitionBlock.LowCyl + 1) * partitionBlock.Surfaces *
            partitionBlock.BlocksPerTrack * 512 / blockSize;
        var data = await TestHelper.ReadData(testCommandHelper, path, partitionOffset, (int)(blocks * blockSize));

        for (var block = 0; block < blocks; block++)
        {
            var offset = block * blockSize;
            if (data[offset + 3] == 2 && data[offset + blockSize - 1] == 1 &&
                data[offset] == 0 && data[offset + 1] == 0 && data[offset + 2] == 0 &&
                data[offset + blockSize - 4] == 0 && data[offset + blockSize - 3] == 0 &&
                data[offset + blockSize - 2] == 0)
            {
                return partitionOffset + offset;
            }
        }

        throw new IOException("Fast file system root block not found");
    }

    /// <summary>
    /// Invert bytes at offset to corrupt data.
    /// </summary>
    protected static async Task Corrupt(TestCommandHelper testCommandHelper, string path, long offset,
        int length = 4)
    {
        var data = await TestHelper.ReadData(testCommandHelper, path, offset, length);
        await TestHelper.WriteData(testCommandHelper, path, offset, data.Select(x => (byte)~x).ToArray());
    }
}
