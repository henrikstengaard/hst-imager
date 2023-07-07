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
    [Fact]
    public async Task WhenExtractingAllRecursivelyFromLzxToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var expectedFileNames = new []{
            "xpkBLZW.library",
            "xpkCBR0.library",
            "xpkCRM2.library",
            "xpkCRMS.library",
            "xpkDHUF.library",
            "xpkDLTA.library",
            "xpkENCO.library",
            "xpkFAST.library",
            "xpkFEAL.library",
            "xpkHFMN.library",
            "xpkHUFF.library",
            "xpkIDEA.library",
            "xpkIMPL.library",
            "xpkLHLB.library",
            "xpkMASH.library",
            "xpkNONE.library",
            "xpkNUKE.library",
            "xpkPWPK.library",
            "xpkRAKE.library",
            "xpkRDCN.library",
            "xpkRLEN.library",
            "xpkSHRI.library",
            "xpkSMPL.library",
            "xpkSQSH.library"
        };
    
        var srcPath = Path.Combine("TestData", "Lzx", "xpk_compress.lzx");
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, true, false, true);

            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);

            // assert - 24 files was extracted
            Assert.Equal(24, files.Length);

            foreach (var expectedFileName in expectedFileNames)
            {
                // assert - expected filename was extracted
                var expectedFilePath = Path.Combine(destPath, expectedFileName);
                Assert.Equal(expectedFilePath, files.FirstOrDefault(x => x.Equals(expectedFilePath, StringComparison.OrdinalIgnoreCase)));
            }
        }
        finally
        {
            DeletePaths(destPath);
        }
    }
    
    [Fact]
    public async Task WhenExtractingAllRecursivelyFromLzxWithWildcardToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    {
        var expectedFileNames = new []{
            "xpkRAKE.library",
            "xpkRDCN.library",
            "xpkRLEN.library",
        };
        
        var srcPath = Path.Combine("TestData", "Lzx", "xpk_compress.lzx");
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "xpkR*"), destPath, true, false, true);
        
            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);
        
            // assert - 3 files was extracted
            Assert.Equal(3, files.Length);

            foreach (var expectedFileName in expectedFileNames)
            {
                // assert - expected filename was extracted
                var expectedFilePath = Path.Combine(destPath, expectedFileName);
                Assert.Equal(expectedFilePath, files.FirstOrDefault(x => x.Equals(expectedFilePath, StringComparison.OrdinalIgnoreCase)));
            }
        }
        finally
        {
            DeletePaths(destPath);
        }
    }
    
    // [Fact]
    // public async Task WhenExtractingAllRecursivelyFromLhaSubdirectoryToLocalDirectoryThenDirectoriesAndFilesAreExtracted()
    // {
    //     var srcPath = Path.Combine("TestData", "Lha", "amiga.lha");
    //     var destPath = $"{Guid.NewGuid()}-extract";
    //
    //     try
    //     {
    //         var fakeCommandHelper = new TestCommandHelper();
    //         var cancellationTokenSource = new CancellationTokenSource();
    //
    //         // arrange - create fs extract command
    //         var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
    //             new List<IPhysicalDrive>(),
    //             Path.Combine(srcPath, "test1"), destPath, true, true);
    //     
    //         // act - extract
    //         var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
    //         Assert.True(result.IsSuccess);
    //
    //         // assert - get extracted files
    //         var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);
    //     
    //         // assert - 3 files was extracted
    //         Assert.Equal(3, files.Length);
    //
    //         // assert - test1.txt file was extracted
    //         var test1Txt = Path.Combine(destPath, "test1.txt");
    //         Assert.Equal(test1Txt, files.FirstOrDefault(x => x.Equals(test1Txt, StringComparison.OrdinalIgnoreCase)));
    //
    //         // assert - test2.info file was extracted
    //         var test2Info = Path.Combine(destPath, "test2.info");
    //         Assert.Equal(test2Info, files.FirstOrDefault(x => x.Equals(test2Info, StringComparison.OrdinalIgnoreCase)));
    //         
    //         // assert - test2.txt file was extracted
    //         var test2Txt = Path.Combine(destPath, "test2", "test2.txt");
    //         Assert.Equal(test2Txt, files.FirstOrDefault(x => x.Equals(test2Txt, StringComparison.OrdinalIgnoreCase)));
    //     }
    //     finally
    //     {
    //         DeletePaths(destPath);
    //     }
    // }

    [Fact]
    public async Task WhenExtractingAFileFromLzxToLocalDirectoryThenFileIsExtracted()
    {
        var srcPath = Path.Combine("TestData", "Lzx", "xpk_compress.lzx");
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                Path.Combine(srcPath, "xpkHUFF.library"), destPath, true, false, true);
        
            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            Assert.True(Directory.Exists(destPath));
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);
        
            Assert.Single(files);
        
            var filePath = Path.Combine(destPath, "xpkHUFF.library");
            Assert.Equal(filePath, files.FirstOrDefault(x => x.Equals(filePath, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(destPath);
        }
    }
    
    // [Fact]
    // public async Task WhenExtractingAFileFromLhaSubdirectoryToLocalDirectoryThenFileIsExtracted()
    // {
    //     var srcPath = Path.Combine("TestData", "Lha", "amiga.lha");
    //     var destPath = $"{Guid.NewGuid()}-extract";
    //
    //     try
    //     {
    //         var fakeCommandHelper = new TestCommandHelper();
    //         var cancellationTokenSource = new CancellationTokenSource();
    //
    //         // arrange - create fs extract command
    //         var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
    //             new List<IPhysicalDrive>(),
    //             Path.Combine(srcPath, "test1", "test2", "test2.txt"), destPath, true, true);
    //     
    //         // act - extract
    //         var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
    //         Assert.True(result.IsSuccess);
    //
    //         // assert - get extracted files
    //         Assert.True(Directory.Exists(destPath));
    //         var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);
    //     
    //         Assert.Single(files);
    //     
    //         var test2TxtPath = Path.Combine(destPath, "test2.txt");
    //         Assert.Equal(test2TxtPath, files.FirstOrDefault(x => x.Equals(test2TxtPath, StringComparison.OrdinalIgnoreCase)));
    //     }
    //     finally
    //     {
    //         DeletePaths(destPath);
    //     }
    // }
}