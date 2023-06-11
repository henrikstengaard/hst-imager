namespace Hst.Imager.Core.Tests.CommandTests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Commands;
using Models.FileSystems;
using Xunit;

public class GivenDirectoryEntryWriter : FsCommandTestBase
{
    [Fact]
    public async Task WhenWriteEntryWithSpaceInFilenameThenSpaceInPreserved()
    {
        // arrange - entry to write
        var entry = new Entry
        {
            Name = "File 1",
            Size = 0,
            Date = DateTime.Now,
            Type = EntryType.File,
            RawPath = "File 1",
            FormattedName = "File 1",
            RelativePathComponents = new []{ "File 1" },
            FullPathComponents = new []{ "File 1" }
        };

        // arrange - path to write entry to
        var path = $"{Guid.NewGuid()}";
        
        try
        {
            // arrange - create directory entry writer
            var writer = new DirectoryEntryWriter(path);
            
            // act - write directory entry
            await writer.WriteEntry(entry, entry.RelativePathComponents, new MemoryStream());
            
            // assert - get written files
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);

            // assert - 1 file was written
            Assert.Single(files);
            
            // assert - file 1 was written
            var file1Path = Path.Combine(path, "File 1");
            Assert.Equal(file1Path,
                files.FirstOrDefault(x => x.Equals(file1Path, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeletePaths(path);
        }
    }
}