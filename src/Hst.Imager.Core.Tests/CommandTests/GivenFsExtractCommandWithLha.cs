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

public class GivenFsExtractCommandWithLha : FsCommandTestBase
{
    [Fact]
    public async Task WhenExtractingAllRecursivelyFromLhaToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = Path.Combine("TestData", "Lha", "amiga.lha");
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, true);
        
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
    public async Task WhenExtractingAllRecursivelyFromLhaWithWildcardToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = Path.Combine("TestData", "Lha", "amiga.lha");
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "test1.*"), destPath, true, true);
        
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
    public async Task WhenExtractingAllRecursivelyFromLhaSubdirectoryToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var srcPath = Path.Combine("TestData", "Lha", "amiga.lha");
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "test1"), destPath, true, true);
        
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
    public async Task WhenExtractingAFileFromLhaToLocalDirectoryThenFileIsExtracted()
    {
        var srcPath = Path.Combine("TestData", "Lha", "amiga.lha");
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "test.txt"), destPath, true, true);
        
            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            Assert.True(Directory.Exists(destPath));
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);
        
            Assert.Single(files);
        
            var file1 = Path.Combine(destPath, "test.txt");
            Assert.Equal(file1, files.FirstOrDefault(x => x.Equals(file1, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(destPath);
        }
    }    
}