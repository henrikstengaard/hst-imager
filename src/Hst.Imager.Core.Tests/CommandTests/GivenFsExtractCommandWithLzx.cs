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

public class GivenFsExtractCommandWithLzx : FsCommandTestBase
{
    private readonly string lzxPath = Path.Combine("TestData", "Lzx", "amiga.lzx");
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenExtractingAllRecursivelyFromLzxToLocalDirectoryThenDirectoriesAndFilesAreExtracted(
        bool recursive)
    {
        var srcPath = $"{Guid.NewGuid()}.lzx";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            // arrange - copy lzx file to src path
            File.Copy(lzxPath, srcPath, true);

            // arrange - create destination directory
            Directory.CreateDirectory(destPath);
            
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, recursive, false, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);
        
            // assert - 5 files was extracted
            Assert.Equal(5, files.Length);

            // assert - test.txt file was extracted
            var testTxt = Path.Combine(destPath, "test.txt");
            Assert.Equal(testTxt, files.FirstOrDefault(x => x.Equals(testTxt, StringComparison.OrdinalIgnoreCase)));

            // assert - test1.info file was extracted
            var test1Info = Path.Combine(destPath, "test1.info");
            Assert.Equal(test1Info, files.FirstOrDefault(x => x.Equals(test1Info, StringComparison.OrdinalIgnoreCase)));
            
            // assert - test1.txt file was extracted
            var test1Txt = Path.Combine(destPath, "test1", "test1.txt");
            Assert.Equal(test1Txt, files.FirstOrDefault(x => x.Equals(test1Txt, StringComparison.OrdinalIgnoreCase)));

            // assert - test2.info file was extracted
            var test2Info = Path.Combine(destPath, "test1", "test2.info");
            Assert.Equal(test2Info, files.FirstOrDefault(x => x.Equals(test2Info, StringComparison.OrdinalIgnoreCase)));
            
            // assert - test2.txt file was extracted
            var test2Txt = Path.Combine(destPath, "test1", "test2", "test2.txt");
            Assert.Equal(test2Txt, files.FirstOrDefault(x => x.Equals(test2Txt, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(destPath);
        }
    }
    
    [Fact]
    public async Task WhenExtractingAllRecursivelyFromLzxWithWildcardToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.lzx";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            // arrange - copy lzx file to src path
            File.Copy(lzxPath, srcPath, true);

            // arrange - create destination directory
            Directory.CreateDirectory(destPath);
            
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "test1.*"), destPath, true, false, true);
        
            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);
        
            // assert - 2 files was extracted
            Assert.Equal(2, files.Length);

            // assert - test1.info file was extracted
            var test1Info = Path.Combine(destPath, "test1.info");
            Assert.Equal(test1Info, files.FirstOrDefault(x => x.Equals(test1Info, StringComparison.OrdinalIgnoreCase)));
            
            // assert - test1.txt file was extracted
            var test1Txt = Path.Combine(destPath, "test1", "test1.txt");
            Assert.Equal(test1Txt, files.FirstOrDefault(x => x.Equals(test1Txt, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(destPath);
        }
    }
    
    [Fact]
    public async Task WhenExtractingAllRecursivelyFromLzxSubdirectoryToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.lzx";
        var destPath = $"{Guid.NewGuid()}-extract";
    
        try
        {
            // arrange - copy lzx file to src path
            File.Copy(lzxPath, srcPath, true);

            // arrange - create destination directory
            Directory.CreateDirectory(destPath);
            
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();
    
            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "test1"), destPath, true, true, true);
        
            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);
    
            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);
        
            // assert - 3 files was extracted
            Assert.Equal(3, files.Length);
    
            // assert - test1.txt file was extracted
            var test1Txt = Path.Combine(destPath, "test1.txt");
            Assert.Equal(test1Txt, files.FirstOrDefault(x => x.Equals(test1Txt, StringComparison.OrdinalIgnoreCase)));
    
            // assert - test2.info file was extracted
            var test2Info = Path.Combine(destPath, "test2.info");
            Assert.Equal(test2Info, files.FirstOrDefault(x => x.Equals(test2Info, StringComparison.OrdinalIgnoreCase)));
            
            // assert - test2.txt file was extracted
            var test2Txt = Path.Combine(destPath, "test2", "test2.txt");
            Assert.Equal(test2Txt, files.FirstOrDefault(x => x.Equals(test2Txt, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(destPath);
        }
    }

    [Fact]
    public async Task WhenExtractingAFileFromLzxToLocalDirectoryThenFileIsExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.lzx";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            // arrange - copy lzx file to src path
            File.Copy(lzxPath, srcPath, true);

            // arrange - create destination directory
            Directory.CreateDirectory(destPath);
            
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "test.txt"), destPath, true, false, true);
        
            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            Assert.True(Directory.Exists(destPath));
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);
        
            Assert.Single(files);
        
            var filePath = Path.Combine(destPath, "test.txt");
            Assert.Equal(filePath, files.FirstOrDefault(x => x.Equals(filePath, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
    
    [Fact]
    public async Task WhenExtractingAFileFromLzxSubdirectoryToLocalDirectoryThenFileIsExtracted()
    {
        var srcPath = $"{Guid.NewGuid()}.lzx";
        var destPath = $"{Guid.NewGuid()}-extract";
    
        try
        {
            // arrange - copy lzx file to src path
            File.Copy(lzxPath, srcPath, true);

            // arrange - create destination directory
            Directory.CreateDirectory(destPath);
            
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();
    
            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), testCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "test1", "test2", "test2.txt"), destPath, true, true, true);
        
            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);
    
            // assert - get extracted files
            Assert.True(Directory.Exists(destPath));
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);
        
            Assert.Single(files);
        
            var test2TxtPath = Path.Combine(destPath, "test2.txt");
            Assert.Equal(test2TxtPath, files.FirstOrDefault(x => x.Equals(test2TxtPath, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(destPath);
        }
    }
    
    [Fact]
    public async Task When_ExtractingAFileFromLzxToLocalDirectoryUsingCapitalLetters_ThenFileIsExtracted()
    {
        // arrange - paths
        var srcPath = $"{Guid.NewGuid()}.lzx";
        var destPath = $"{Guid.NewGuid()}-extract";
        var extractPath = Path.Combine(srcPath, "DIR1", "FILE1.TXT");

        try
        {
            // arrange - copy lzx file to src path
            File.Copy(Path.Combine("TestData", "Lzx", "dirs-files.lzx"), srcPath);

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
                Path.Combine(destPath, "file1.txt")
            ];
            Assert.Equal(expectedFiles, actualFiles);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
}