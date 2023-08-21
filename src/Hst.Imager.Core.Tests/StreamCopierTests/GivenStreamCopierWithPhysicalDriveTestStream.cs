using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Extensions;
using Xunit;

namespace Hst.Imager.Core.Tests.StreamCopierTests;

public class GivenStreamCopierWithPhysicalDriveTestStream
{
    [Fact]
    public async Task WhenCopyStreamNotMatchingStreamCopierBufferSizeThenCopiedDataIsIdentical()
    {
        var srcData = TestDataHelper.CreateTestData(10.MB().ToSectorSize());
        var baseStream = new MemoryStream(srcData);
        await using var srcStream = new SectorStream(new PhysicalDriveTestStream(baseStream));
        using var destStream = new MemoryStream();

        var tokenSource = new CancellationTokenSource();
        var streamCopier = new StreamCopier();
        await streamCopier.Copy(tokenSource.Token, srcStream, destStream, srcData.Length);

        var destData = destStream.ToArray();
        Assert.Equal(srcData.Length, destData.Length);
        Assert.Equal(srcData, destData);
    }
}