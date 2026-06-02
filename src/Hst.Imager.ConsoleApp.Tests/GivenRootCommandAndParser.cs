using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

namespace Hst.Imager.ConsoleApp.Tests
{
    public class GivenRootCommandAndParser
    {
        [Fact]
        public async Task When_InvokingConsoleCommands_Then_NoExceptionIsThrown()
        {
            // arrange - create root command
            var rootCommand = CommandFactory.CreateRootCommand();

            // act & assert - invoke subcommands in root command and assert no exception is thrown
            foreach (var command in rootCommand.Subcommands)
            {
                try
                {
                    var parseResult = rootCommand.Parse([command.Name]);
                    await parseResult.InvokeAsync(new InvocationConfiguration());
                }
                catch (Exception e)
                {
                    Assert.Fail(e.Message);
                }
            }
        }

        [Fact]
        public async Task When_InvokingInfoCommandWithEmptyImageFile_Then_NoErrorIsReturned()
        {
            // arrange - paths
            var imgPath = $"{Guid.NewGuid()}.img";

            // arrange - create root command
            var rootCommand = CommandFactory.CreateRootCommand();

            try
            {
                // arrange - create empty image file
                await File.WriteAllBytesAsync(imgPath, Array.Empty<byte>());

                // act - info command with image file
                var parseResult = rootCommand.Parse(new[] { "info", imgPath });
                var errorCode = await parseResult.InvokeAsync(new InvocationConfiguration());

                // assert - info command returned 0
                Assert.Equal(0, errorCode);
            }
            catch(Exception e)
            {
                Assert.Fail(e.Message);
            }
            finally
            {
                // delete image file, if it exists
                if (File.Exists(imgPath))
                {
                    File.Delete(imgPath);
                }
            }
        }
    }
}