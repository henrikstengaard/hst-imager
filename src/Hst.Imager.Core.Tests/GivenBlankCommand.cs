namespace Hst.Imager.Core.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Commands;
    using Models;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;
    using File = System.IO.File;

    public class GivenBlankCommand : CommandTestBase
    {
        [Fact]
        public async Task WhenCreateBlankImgThenDataIzZeroFilled()
        {
            // arrange
            var path = $"{Guid.NewGuid()}.img";
            var size = new Size(512 * 512, Unit.Bytes);
            var testCommandHelper = new TestCommandHelper();
            testCommandHelper.AddTestMedia(path);
            var cancellationTokenSource = new CancellationTokenSource();

            // act - create blank
            var blankCommand = new BlankCommand(new NullLogger<BlankCommand>(), testCommandHelper, path, size, false);
            var result = await blankCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert data is zero filled
            var sourceBytes = new byte[Convert.ToInt64(size.Value)];
            var destinationBytes = testCommandHelper.GetTestMedia(path).Data;
            Assert.Equal(sourceBytes, destinationBytes);
        }

        [Fact]
        public async Task WhenCreateBlankVhdThenDataIzZeroFilled()
        {
            // arrange
            var path = $"{Guid.NewGuid()}.vhd";
            var size = new Size(512 * 512, Unit.Bytes);
            var testCommandHelper = new TestCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // act - create blank
            var blankCommand = new BlankCommand(new NullLogger<BlankCommand>(), testCommandHelper, path, size, false);
            var result = await blankCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // get destination bytes from vhd
            var destinationBytes = await ReadMediaBytes(testCommandHelper, path, Convert.ToInt64(size.Value));
            var destinationPathSize = new FileInfo(path).Length;

            // assert vhd is less than size
            Assert.True(destinationPathSize < size.Value);

            // assert data is zero filled
            var sourceBytes = new byte[Convert.ToInt64(size.Value)];
            Assert.Equal(sourceBytes, destinationBytes);

            // delete vhd file
            File.Delete(path);
        }
    }
}