namespace Hst.Imager.Core.Tests.CommandTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amiga.FileSystems.FastFileSystem;
using Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Directory = System.IO.Directory;
using File = System.IO.File;

public class GivenFsExtractCommandWithAdfAndWindowsReservedNamesInFiles : FsCommandTestBase
{
    [Fact]
    public async Task WhenExtractingAFileWithWindowsReservedNameToLocalDirectoryThenFileIsRenamedAndExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.adf";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            await CreateDos3FormattedAdf(srcPath);
            await CreateFilesWithWindowsReservedNames(srcPath);

            using var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true, UaeMetadatas.UaeMetadata.None);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);

            // assert - 2 file was extracted
            Assert.Equal(2, files.Length);

            var windowsReservedPrefix = OperatingSystem.IsWindows() ? "_" : string.Empty;

            // assert - aux file was extracted
            var auxPath = Path.Combine(destPath, $"{windowsReservedPrefix}AUX");
            Assert.Equal(auxPath,
                files.FirstOrDefault(x => x.Equals(auxPath, StringComparison.OrdinalIgnoreCase)));

            // assert - aux.info file was extracted
            var auxInfoPath = Path.Combine(destPath, $"{windowsReservedPrefix}AUX.info");
            Assert.Equal(auxInfoPath,
                files.FirstOrDefault(x => x.Equals(auxInfoPath, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    private async Task CreateFilesWithWindowsReservedNames(string path)
    {
        await using var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite);
        
        await using var volume = await FastFileSystemVolume.MountAdf(stream);

        await volume.CreateFile("AUX");
        await volume.CreateFile("AUX.info");
    }
}