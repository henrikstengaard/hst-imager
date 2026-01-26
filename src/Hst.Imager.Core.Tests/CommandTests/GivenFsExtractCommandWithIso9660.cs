using Hst.Imager.Core.UaeMetadatas;

namespace Hst.Imager.Core.Tests.CommandTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class GivenFsExtractCommandWithIso9660 : FsCommandTestBase
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenExtractingAllRecursivelyFromIsoToLocalDirectoryThenDirectoriesAndFilesAreExtracted(
        bool recursive)
    {
        var srcPath = $"{Guid.NewGuid()}.iso";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            // arrange - create iso9660 image with directories and files
            CreateIso9660WithDirectoriesAndFiles(srcPath);

            // arrange - create destination directory
            Directory.CreateDirectory(destPath);
            
            using var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories).OrderBy(x => x).ToArray();

            // assert - 5 files was extracted
            Assert.Equal(5, files.Length);

            // assert - files are extracted
            var expectedFiles = new[]
            {
                Path.Combine(destPath, "dir1", "dir2", "file4.txt"),
                Path.Combine(destPath, "dir1", "file3.txt"),
                Path.Combine(destPath, "dir1", "test.txt"),
                Path.Combine(destPath, "file1.txt"),
                Path.Combine(destPath, "file2.txt")
            };
            Assert.Equal(expectedFiles, files);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    [Fact]
    public async Task WhenExtractingAllRecursivelyFromIsoSubdirectoryToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.iso";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            // arrange - create iso9660 image with directories and files
            CreateIso9660WithDirectoriesAndFiles(srcPath);

            // arrange - create destination directory
            Directory.CreateDirectory(destPath);
            
            using var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "dir1"), destPath, true, false, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories).OrderBy(x => x).ToArray();

            // assert - 3 files was extracted
            Assert.Equal(3, files.Length);

            // assert - files are extracted
            var expectedFiles = new[]
            {
                Path.Combine(destPath, "dir2", "file4.txt"),
                Path.Combine(destPath, "file3.txt"),
                Path.Combine(destPath, "test.txt")
            };
            Assert.Equal(expectedFiles, files);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
    
    [Fact]
    public async Task WhenExtractingAllRecursivelyFromIsoWithWildcardToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.iso";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            // arrange - create iso9660 image with directories and files
            CreateIso9660WithDirectoriesAndFiles(srcPath);

            // arrange - create destination directory
            Directory.CreateDirectory(destPath);
            
            using var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "file*.txt"), destPath, true, false, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories).OrderBy(x => x).ToArray();

            // assert - 4 files was extracted
            Assert.Equal(4, files.Length);

            // assert - files are extracted
            var expectedFiles = new[]
            {
                Path.Combine(destPath, "dir1", "dir2", "file4.txt"),
                Path.Combine(destPath, "dir1", "file3.txt"),
                Path.Combine(destPath, "file1.txt"),
                Path.Combine(destPath, "file2.txt")
            };
            Assert.Equal(expectedFiles, files);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
    
    [Fact]
    public async Task WhenExtractingAFileFromIsoToLocalDirectoryThenFileIsExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.iso";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            // arrange - create iso9660 image with directories and files
            CreateIso9660WithDirectoriesAndFiles(srcPath);

            // arrange - create destination directory
            Directory.CreateDirectory(destPath);
            
            using var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "file1.txt"), destPath, true, false, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories).OrderBy(x => x).ToArray();

            // assert - 1 file was extracted
            Assert.Single(files);

            // assert - file is extracted
            var expectedFiles = new[]
            {
                Path.Combine(destPath, "file1.txt"),
            };
            Assert.Equal(expectedFiles, files);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    [Fact]
    public async Task WhenExtractingAFileFromIsoSubdirectoryToLocalDirectoryThenFileIsExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.iso";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            // arrange - create iso9660 image with directories and files
            CreateIso9660WithDirectoriesAndFiles(srcPath);

            // arrange - create destination directory
            Directory.CreateDirectory(destPath);
            
            using var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "dir1", "file3.txt"), destPath, true, false, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories).OrderBy(x => x).ToArray();

            // assert - 1 file was extracted
            Assert.Single(files);

            // assert - file is extracted
            var expectedFiles = new[]
            {
                Path.Combine(destPath, "file3.txt"),
            };
            Assert.Equal(expectedFiles, files);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }

    [Fact]
    public async Task When_ExtractingWithReservedWindowsFilenamesAndNoMetaData_Then_FilesAreExtractedWithoutMetaData()
    {
        var srcPath = $"{Guid.NewGuid()}.iso";
        var destPath = $"{Guid.NewGuid()}-extract";
        const UaeMetadata uaeMetadata = UaeMetadata.None;

        try
        {
            // arrange - create iso9660 image with reserved windows filename
            File.Copy(Path.Combine("TestData", "iso", "reserved_windows_filenames.iso"), srcPath);
            
            // arrange - create destination directory
            Directory.CreateDirectory(destPath);
            
            using var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true, uaeMetadata: uaeMetadata);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var windowsReservedPrefix = OperatingSystem.IsWindows() ? "_" : string.Empty;
            var expectedFiles = new[]
            {
                Path.Combine(destPath, $"{windowsReservedPrefix}AUX"),
                Path.Combine(destPath, $"{windowsReservedPrefix}AUX.info"),
            };
            var actualFiles = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories).OrderBy(x => x).ToArray();
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
    
    [Fact]
    public async Task When_ExtractingWithReservedWindowsFilenamesAndUaeFsDb_Then_FilesAreExtractedWithMetaData()
    {
        var srcPath = $"{Guid.NewGuid()}.iso";
        var destPath = $"{Guid.NewGuid()}-extract";
        const UaeMetadata uaeMetadata = UaeMetadata.UaeFsDb;

        try
        {
            // arrange - create iso9660 image with reserved windows filename
            File.Copy(Path.Combine("TestData", "iso", "reserved_windows_filenames.iso"), srcPath);
            
            // arrange - create destination directory
            Directory.CreateDirectory(destPath);
            
            using var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true, uaeMetadata: uaeMetadata);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var expectedFiles = OperatingSystem.IsWindows()
                ? new[]
                {
                    Path.Combine(destPath, "__uae___AUX"),
                    Path.Combine(destPath, "__uae___AUX.info"),
                    Path.Combine(destPath, "_UAEFSDB.___"),
                } : new []
                {
                    Path.Combine(destPath, "AUX"),
                    Path.Combine(destPath, "AUX.info")
                };
            var actualFiles = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories).OrderBy(x => x).ToArray();
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
    
    [Fact]
    public async Task When_ExtractingWithReservedWindowsFilenamesAndUaeMetafile_Then_FilesAreExtractedWithMetaData()
    {
        var srcPath = $"{Guid.NewGuid()}.iso";
        var destPath = $"{Guid.NewGuid()}-extract";
        const UaeMetadata uaeMetadata = UaeMetadata.UaeMetafile;

        try
        {
            // arrange - create iso9660 image with reserved windows filename
            File.Copy(Path.Combine("TestData", "iso", "reserved_windows_filenames.iso"), srcPath);
            
            // arrange - create destination directory
            Directory.CreateDirectory(destPath);
            
            using var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true, uaeMetadata: uaeMetadata);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var expectedFiles = OperatingSystem.IsWindows()
                ? new[]
                {
                    Path.Combine(destPath, "%41%55%58"),
                    Path.Combine(destPath, "%41%55%58.uaem"),
                    Path.Combine(destPath, "%41%55%58%2e%69%6e%66%6f"),
                    Path.Combine(destPath, "%41%55%58%2e%69%6e%66%6f.uaem")
                } : new []
                {
                    Path.Combine(destPath, "AUX"),
                    Path.Combine(destPath, "AUX.info")
                };
            var actualFiles = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories).OrderBy(x => x).ToArray();
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
    
    [Fact]
    public async Task When_ExtractingAFileFromIsoToLocalDirectoryUsingCapitalLetters_ThenFileIsExtracted()
    {
        // arrange - paths
        var srcPath = $"{Guid.NewGuid()}.iso";
        var destPath = $"{Guid.NewGuid()}-extract";
        var extractPath = Path.Combine(srcPath, "DIR1", "FILE3.TXT");

        try
        {
            // arrange - create iso9660 image with directories and files
            CreateIso9660WithDirectoriesAndFiles(srcPath);

            // arrange - create destination directory
            Directory.CreateDirectory(destPath);
            
            using var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                extractPath, destPath, true, false, true);
        
            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            Assert.True(Directory.Exists(destPath));
            var actualFiles = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);
        
            // assert - single file is extracted
            Assert.Single(actualFiles);
            string[] expectedFiles =
            [
                Path.Combine(destPath, "file3.txt")
            ];
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
}