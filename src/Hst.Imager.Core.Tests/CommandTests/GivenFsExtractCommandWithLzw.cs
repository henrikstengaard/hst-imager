namespace Hst.Imager.Core.Tests.CommandTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Commands;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class GivenFsExtractCommandWithLzw : FsCommandTestBase
{
    [Fact]
    public async Task WhenExtractingLzwFileToLocalDirectoryThenFileIsExtracted()
    {
        var srcName = Guid.NewGuid().ToString();
        var srcPath = $"{srcName}.Z";
        var destPath = $"{Guid.NewGuid()}-extract";

        try
        {
            File.Copy(Path.Combine("TestData", "Lzw", "test.txt.Z"), srcPath);

            var fakeCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // arrange - create fs extract command
            var fsExtractCommand = new FsExtractCommand(new NullLogger<FsExtractCommand>(), fakeCommandHelper,
                new List<IPhysicalDrive>(),
                srcPath, destPath, false, false, true);
            
            // act - extract
            var result = await fsExtractCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert - get extracted files
            var files = Directory.GetFiles(destPath, "*.*", SearchOption.AllDirectories);

            // assert - 1 file was extracted
            Assert.Single(files);

            // assert - src file was extracted
            var srcFile = Path.Combine(destPath, srcName);
            Assert.Equal(srcFile, files.FirstOrDefault(x => x.Equals(srcFile, StringComparison.OrdinalIgnoreCase)));
            
            // assert - extract
            var expectedText = await File.ReadAllTextAsync(Path.Combine("TestData", "Lzw", "test.txt"), cancellationTokenSource.Token);
            if (OperatingSystem.IsWindows())
            {
                expectedText = expectedText.Replace("\r\n", "\n");
            }
            var actualText = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(srcFile, cancellationTokenSource.Token));
            Assert.Equal(expectedText, actualText);
        }
        finally
        {
            DeletePaths(srcPath, destPath);
        }
    }
}