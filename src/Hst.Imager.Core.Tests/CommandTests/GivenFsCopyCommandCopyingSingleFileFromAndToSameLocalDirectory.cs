using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests;

public class GivenFsCopyCommandCopyingSingleFileFromAndToSameLocalDirectory : FsCommandTestBase
{
    [Fact]
    public async Task When_CopyingFromAndToSameRootDirLocalDirectoryMedia_Then_FileIsCopied()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "file2.txt");
        var destPath = Path.Combine(mediaPath, "file2_copy.txt");
        const bool recursive = false;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "file2.txt"), []);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);
            
            // assert - directories exist
            var expectedDirs = new[]
            {
                Path.Combine(mediaPath, "dir1"),
                Path.Combine(mediaPath, "dir1", "dir3"),
                Path.Combine(mediaPath, "dir2")
            };
            var actualDirs = Directory.GetDirectories(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualDirs);
            Assert.Equal(expectedDirs, actualDirs);
            
            var expectedFiles = new[]
            {
                Path.Combine(mediaPath, "dir1", "file1.txt"),
                Path.Combine(mediaPath, "file2_copy.txt"),
                Path.Combine(mediaPath, "file2.txt")
            };
            var actualFiles = Directory.GetFiles(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingFromAndToSameRootDirLocalDirectoryMediaFileExists_Then_ErrorIsReturned()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "file2.txt");
        var destPath = Path.Combine(mediaPath, "file2_copy.txt");
        const bool recursive = false;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "file2.txt"), []);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "file2_copy.txt"), []);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);

            // assert - error is returned
            Assert.False(result.IsSuccess);
            Assert.True(result.IsFaulted);
            Assert.NotNull(result.Error);
            Assert.IsType<FileExistsError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingFromAndToSameRootDirLocalDirectoryMediaFileExistsWithForce_Then_FileIsCopied()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "file2.txt");
        var destPath = Path.Combine(mediaPath, "file2_copy.txt");
        const bool recursive = false;
        const bool force = true;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "file2.txt"), []);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "file2_copy.txt"), []);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true, forceOverwrite: force);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);
            
            // assert - directories exist
            var expectedDirs = new[]
            {
                Path.Combine(mediaPath, "dir1"),
                Path.Combine(mediaPath, "dir1", "dir3"),
                Path.Combine(mediaPath, "dir2")
            };
            var actualDirs = Directory.GetDirectories(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualDirs);
            Assert.Equal(expectedDirs, actualDirs);
            
            var expectedFiles = new[]
            {
                Path.Combine(mediaPath, "dir1", "file1.txt"),
                Path.Combine(mediaPath, "file2_copy.txt"),
                Path.Combine(mediaPath, "file2.txt")
            };
            var actualFiles = Directory.GetFiles(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingFromAndToSameRootDirLocalDirectoryMediaFileExistsIsAdf_Then_FileIsCopied()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "file2.txt");
        var destPath = Path.Combine(mediaPath, "file2_copy.txt");
        const bool recursive = false;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "file2.txt"), []);

            // arrange - create existing file2 copy as adf
            var adfBytes = new byte[Amiga.FloppyDiskConstants.DoubleDensity.Size];
            Array.Copy(MagicBytes.AdfDosMagicNumber, 0, adfBytes, 0,
                MagicBytes.AdfDosMagicNumber.Length);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "file2_copy.txt"), adfBytes);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            
            // assert - error is returned
            Assert.False(result.IsSuccess);
            Assert.True(result.IsFaulted);
            Assert.NotNull(result.Error);
            Assert.IsType<FileExistsError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingFromAndToSameRootDirLocalDirectoryMediaFileExistsIsAdfWithForce_Then_FileIsCopied()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "file2.txt");
        var destPath = Path.Combine(mediaPath, "file2_copy.txt");
        const bool recursive = false;
        const bool force = true;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "file2.txt"), []);

            // arrange - create existing file2 copy as adf
            var adfBytes = new byte[Amiga.FloppyDiskConstants.DoubleDensity.Size];
            Array.Copy(MagicBytes.AdfDosMagicNumber, 0, adfBytes, 0,
                MagicBytes.AdfDosMagicNumber.Length);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "file2_copy.txt"), adfBytes);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true, forceOverwrite: force);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);
            
            // assert - directories exist
            var expectedDirs = new[]
            {
                Path.Combine(mediaPath, "dir1"),
                Path.Combine(mediaPath, "dir1", "dir3"),
                Path.Combine(mediaPath, "dir2")
            };
            var actualDirs = Directory.GetDirectories(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualDirs);
            Assert.Equal(expectedDirs, actualDirs);
            
            var expectedFiles = new[]
            {
                Path.Combine(mediaPath, "dir1", "file1.txt"),
                Path.Combine(mediaPath, "file2_copy.txt"),
                Path.Combine(mediaPath, "file2.txt")
            };
            var actualFiles = Directory.GetFiles(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_CopyingFromAndToSameRootDirLocalDirectoryMediaFileExistsIsMbrImg_Then_FileIsCopied()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "file2.txt");
        var destPath = Path.Combine(mediaPath, "file2_copy.txt");
        const bool recursive = false;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "file2.txt"), []);

            // arrange - create existing file2 copy as mbr img
            var mbrBytes = new byte[512];
            Array.Copy(MagicBytes.MbrMagicNumber, 0, mbrBytes, 0x1fe,
                MagicBytes.MbrMagicNumber.Length);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "file2_copy.txt"), mbrBytes);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            
            // assert - error is returned
            Assert.False(result.IsSuccess);
            Assert.True(result.IsFaulted);
            Assert.NotNull(result.Error);
            Assert.IsType<FileExistsError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_CopyingFromAndToSameRootDirLocalDirectoryMediaFileExistsIsMbrImgWithForce_Then_FileIsCopied()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "file2.txt");
        var destPath = Path.Combine(mediaPath, "file2_copy.txt");
        const bool recursive = false;
        const bool force = true;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "file2.txt"), []);

            // arrange - create existing file2 copy as mbr img
            var mbrBytes = new byte[512];
            Array.Copy(MagicBytes.MbrMagicNumber, 0, mbrBytes, 0x1fe,
                MagicBytes.MbrMagicNumber.Length);
            await File.WriteAllBytesAsync(Path.Combine(mediaPath, "file2_copy.txt"), mbrBytes);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true, forceOverwrite: true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);
            
            // assert - directories exist
            var expectedDirs = new[]
            {
                Path.Combine(mediaPath, "dir1"),
                Path.Combine(mediaPath, "dir1", "dir3"),
                Path.Combine(mediaPath, "dir2")
            };
            var actualDirs = Directory.GetDirectories(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualDirs);
            Assert.Equal(expectedDirs, actualDirs);
            
            var expectedFiles = new[]
            {
                Path.Combine(mediaPath, "dir1", "file1.txt"),
                Path.Combine(mediaPath, "file2_copy.txt"),
                Path.Combine(mediaPath, "file2.txt")
            };
            var actualFiles = Directory.GetFiles(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    
    [Fact]
    public async Task When_CopyingToDirFromAndToSameLocalMedia_Then_FileIsCopied()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir1", "dir3");
        const bool recursive = false;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);
            
            // assert - directories exist
            var expectedDirs = new[]
            {
                Path.Combine(mediaPath, "dir1"),
                Path.Combine(mediaPath, "dir1", "dir3"),
                Path.Combine(mediaPath, "dir2")
            };
            var actualDirs = Directory.GetDirectories(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualDirs);
            Assert.Equal(expectedDirs, actualDirs);
            
            var expectedFiles = new[]
            {
                Path.Combine(mediaPath, "dir1", "dir3", "file1.txt"),
                Path.Combine(mediaPath, "dir1", "file1.txt")
            };
            var actualFiles = Directory.GetFiles(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingToNewNameFromAndToSameLocalMedia_Then_FileIsCopied()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir1", "file1_copy.txt");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, false, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);
            
            // assert - directories exist
            var expectedDirs = new[]
            {
                Path.Combine(mediaPath, "dir1"),
                Path.Combine(mediaPath, "dir1", "dir3"),
                Path.Combine(mediaPath, "dir2")
            };
            var actualDirs = Directory.GetDirectories(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualDirs);
            Assert.Equal(expectedDirs, actualDirs);
            
            var expectedFiles = new[]
            {
                Path.Combine(mediaPath, "dir1", "file1_copy.txt"),
                Path.Combine(mediaPath, "dir1", "file1.txt")
            };
            var actualFiles = Directory.GetFiles(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingToNonExistingRootDirectoryFromAndToSameLocalMedia_Then_FileIsCopiedAndRenamed()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir4");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, false, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            Assert.True(result.IsSuccess);
            
            // assert - root directory contains 2 dir entries
            var expectedDirs = new[]
            {
                Path.Combine(mediaPath, "dir1"),
                Path.Combine(mediaPath, "dir1", "dir3"),
                Path.Combine(mediaPath, "dir2")
            };
            var actualDirs = Directory.GetDirectories(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualDirs);
            Assert.Equal(expectedDirs, actualDirs);
            
            // assert - root directory contains 1 file entry
            var expectedFiles = new[]
            {
                Path.Combine(mediaPath, "dir1", "file1.txt"),
                Path.Combine(mediaPath, "dir4")
            };
            var actualFiles = Directory.GetFiles(mediaPath, "*.*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }

    [Fact]
    public async Task When_CopyingToNonExistingSubDirectoryFromAndToSameLocalMedia_Then_ErrorIsReturned()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir4", "dir5");

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            
            // assert - error is returned
            Assert.False(result.IsSuccess);
            Assert.True(result.IsFaulted);
            Assert.NotNull(result.Error);
            Assert.IsType<PathNotFoundError>(result.Error);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
    
    [Fact]
    public async Task When_CopyingToNonExistingSubDirectoryFromAndToSameLocalMediaWithCreateDestDir_Then_FileIsCopied()
    {
        var mediaPath = Guid.NewGuid().ToString();
        var srcPath = Path.Combine(mediaPath, "dir1", "file1.txt");
        var destPath = Path.Combine(mediaPath, "dir4", "dir5");
        const bool createDestDir = true;

        try
        {
            // arrange - test command helper
            using var testCommandHelper = new TestCommandHelper();

            // arrange - create directories and files
            await LocalTestHelper.CreateDirectoriesAndFiles(mediaPath);
            
            // arrange - create fs copy command
            var fsCopyCommand = new FsCopyCommand(new NullLogger<FsCopyCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(), srcPath, destPath, true, false, true,
                makeDirectory: createDestDir);
            
            // act - copy
            var result = await fsCopyCommand.Execute(CancellationToken.None);
            
            // assert - copy is successful
            Assert.True(result.IsSuccess);
            
            // assert - dir4, dir5 directory contains 1 file entry
            var expectedFiles = new[]
            {
                Path.Combine(mediaPath, "dir4", "dir5", "file1.txt"),
            };
            var actualFiles = Directory.GetFiles(Path.Combine(mediaPath, "dir4", "dir5"), "*.*", SearchOption.AllDirectories);
            Array.Sort(actualFiles);
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(mediaPath);
        }
    }
}