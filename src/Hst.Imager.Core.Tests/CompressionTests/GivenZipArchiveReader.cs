using Hst.Imager.Core.Compressions.Zip;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Hst.Imager.Core.Tests.CompressionTests
{
    public class GivenZipArchiveReader
    {
        [Fact]
        public async Task When_ReadFromZipArchive_Then_HeadersAreRead()
        {
            // arrange
            var zipPath = Path.Combine("TestData", "Zip", "amiga.zip");
            var zipStream = new MemoryStream(await File.ReadAllBytesAsync(zipPath));
            var zipArchiveReader = new ZipArchiveReader(zipStream);

            // act - read zip headers
            var zipHeaders = new List<IZipHeader>();
            IZipHeader zipHeader;
            while ((zipHeader = await zipArchiveReader.Read()) != null)
            {
                zipHeaders.Add(zipHeader);
            }

            // assert - 15 zip headers read
            Assert.Equal(15, zipHeaders.Count);

            // assert - local file headers are read
            var expectedLocalFileHeaderFileNames = new[]
            {
                "test1/",
                "test1/test2/",
                "test1/test2/test2.txt",
                "test1/test2.info",
                "test1/test1.txt",
                "test.txt",
                "test1.info"
            };
            var localFileHeaderFileNames = zipHeaders
                .OfType<LocalFileHeader>()
                .Select(x => x.FileName)
                .ToList();
            Assert.Equal(expectedLocalFileHeaderFileNames, localFileHeaderFileNames);

            // assert - central directory file headers are read
            var expectedCentralDirectoryFileHeaderFileNames = new[]
            {
                "test1/",
                "test1/test2/",
                "test1/test2/test2.txt",
                "test1/test2.info",
                "test1/test1.txt",
                "test.txt",
                "test1.info"
            };
            var centralDirectoryFileHeaderFileNames = zipHeaders
                .OfType<CentralDirectoryFileHeader>()
                .Select(x => x.FileName)
                .ToList();
            Assert.Equal(expectedCentralDirectoryFileHeaderFileNames, centralDirectoryFileHeaderFileNames);

            // assert - end of central directory file header is read
            var endOfCentralDirectoryFileHeaders = zipHeaders
                .OfType<EndOfCentralDirectoryFileHeader>()
                .ToList();
            Assert.Single(endOfCentralDirectoryFileHeaders);

            zipStream.Position = 0;
            var zipArchive = new ZipArchive(zipStream);
        }
    }
}
