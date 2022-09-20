namespace Hst.Imager.Core.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Imager.Core.Commands;
    using Hst.Imager.Core.Models;
    using Microsoft.Extensions.Logging.Abstractions;
    using Xunit;

    public class GivenBlankCommand : CommandTestBase
    {
        [Fact]
        public async Task WhenCreateBlankImgThenDataIzZeroFilled()
        {
            // arrange
            var path = $"{Guid.NewGuid()}.img";
            var size = new Size(512 * 512, Unit.Bytes);
            var fakeCommandHelper = new FakeCommandHelper(writeableMediaPaths: new[] { path });
            var cancellationTokenSource = new CancellationTokenSource();

            // act - create blank
            var blankCommand = new BlankCommand(new NullLogger<BlankCommand>(), fakeCommandHelper, path, size, false);
            var result = await blankCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // assert data is zero filled
            var sourceBytes = new byte[Convert.ToInt64(size.Value)];
            var destinationBytes = fakeCommandHelper.GetMedia(path).GetBytes();
            Assert.Equal(sourceBytes, destinationBytes);
        }

        [Fact]
        public async Task WhenCreateBlankVhdThenDataIzZeroFilled()
        {
            // arrange
            var path = $"{Guid.NewGuid()}.vhd";
            var size = new Size(512 * 512, Unit.Bytes);
            var fakeCommandHelper = new FakeCommandHelper();
            var cancellationTokenSource = new CancellationTokenSource();

            // act - create blank
            var blankCommand = new BlankCommand(new NullLogger<BlankCommand>(), fakeCommandHelper, path, size, false);
            var result = await blankCommand.Execute(cancellationTokenSource.Token);
            Assert.True(result.IsSuccess);

            // get destination bytes from vhd
            var destinationBytes = await ReadMediaBytes(fakeCommandHelper, path, Convert.ToInt64(size.Value));
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