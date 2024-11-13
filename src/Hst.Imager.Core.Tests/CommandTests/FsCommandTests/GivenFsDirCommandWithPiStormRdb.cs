using DiscUtils.Partitions;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.FsCommandTests
{
    public class GivenFsDirCommandWithPiStormRdb : FsCommandTestBase
    {
        [Fact]
        public async Task When_ListingEntryInPiStormRdb_Then_RdbPartitionsAreListed()
        {
            // arrange - paths
            var mbrDiskPath = $"mbr_{Guid.NewGuid()}.vhd";
            var dirPath = Path.Combine(mbrDiskPath, "mbr", "2", "rdb");
            const bool recursive = false;

            // arrange - test command helper
            var testCommandHelper = new TestCommandHelper();

            // arrange - create mdr disk
            await CreateMbrDiskWithFat16AndPiStormRdbPartitions(testCommandHelper, mbrDiskPath);

            // arrange - create fs dir command
            EntriesInfo entriesInfo = null;
            var fsDirCommand = new FsDirCommand(new NullLogger<FsDirCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                dirPath, recursive);
            fsDirCommand.EntriesRead += (_, e) =>
            {
                entriesInfo = e.EntriesInfo;
            };

            // act - execute fs dir command
            var cancellationTokenSource = new CancellationTokenSource();
            var result = await fsDirCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - 1 entry is listed
            Assert.NotNull(entriesInfo);
            var entries = entriesInfo.Entries.ToList();
            Assert.Single(entries);

            // assert - rdb partition dh0 is listed as directory
            var expectedDirNames = new[]
            {
                "DH0"
            };
            var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
            Assert.Equal(expectedDirNames, dirNames);
        }

        private async Task CreateMbrDiskWithFat16AndPiStormRdbPartitions(TestCommandHelper testCommandHelper, string mbrDiskPath)
        {
            // disk sizes
            var mbrDiskSize = 100.MB();
            var rdbDiskSize = 20.MB();

            // add mbr disk media
            testCommandHelper.AddTestMedia(mbrDiskPath, mbrDiskSize);

            // add rdb disk media
            var rdbDiskPath = $"rdb_{Guid.NewGuid()}.vhd";
            testCommandHelper.AddTestMedia(rdbDiskPath, rdbDiskSize);

            // calculate mbr parttion start and end sectors
            var mbrPartition1StartSector = 63;
            var mbrPartition1EndSector = mbrPartition1StartSector + 16384;
            var mbrPartition2StartSector = mbrPartition1EndSector + 1;
            var mbrPartition2EndSector = (mbrDiskSize / 512) - 10;

            // mbr disk
            await CreateMbrDisk(testCommandHelper, mbrDiskPath, mbrDiskSize);
            await AddMbrPartition(testCommandHelper, mbrDiskPath,
                mbrPartition1StartSector, mbrPartition1EndSector, BiosPartitionTypes.Fat16);
            await AddMbrPartition(testCommandHelper, mbrDiskPath,
                mbrPartition2StartSector, mbrPartition2EndSector, Constants.BiosPartitionTypes.PiStormRdb);

            // rdb disk
            await CreatePfs3FormattedDisk(testCommandHelper, rdbDiskPath, rdbDiskSize);

            // get readable media for rdb disk
            var rdbMediaResult = await testCommandHelper.GetReadableMedia(Enumerable.Empty<IPhysicalDrive>(), rdbDiskPath);
            Assert.True(rdbMediaResult.IsSuccess);

            // get writable media for mbr disk
            var mbrMediaResult = await testCommandHelper.GetWritableMedia(Enumerable.Empty<IPhysicalDrive>(), mbrDiskPath);
            Assert.True(mbrMediaResult.IsSuccess);

            // copy rdb media to mbr partition 2 creating pistorm rdb hard disk
            using (var mbrMedia = mbrMediaResult.Value)
            {
                var mbrStream = mbrMedia is DiskMedia diskMedia
                    ? diskMedia.Disk.Content
                    : mbrMedia.Stream;

                mbrStream.Seek(512 * mbrPartition2StartSector, SeekOrigin.Begin);

                using var rdbMedia = rdbMediaResult.Value;

                var rdbStream = rdbMedia is DiskMedia rdbDiskMedia
                    ? rdbDiskMedia.Disk.Content
                    : rdbMedia.Stream;

                rdbStream.Position = 0;
                var buffer = new byte[4096];

                int bytesRead;
                do
                {
                    bytesRead = rdbStream.Read(buffer, 0, buffer.Length);
                    mbrStream.Write(buffer, 0, bytesRead);
                } while (bytesRead != 0);
            }
        }
    }
}