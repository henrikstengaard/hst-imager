using System;
using System.Threading.Tasks;
using Hst.Imager.Core.Models.FileSystems;
using Xunit;

namespace Hst.Imager.Core.Tests.EntryWriterTests;

public class GivenEntryWriterInitializedWithZeroRootPathComponents
{
    private readonly string[] rootPathComponents = [];

    [Theory]
    [InlineData(EntryWriterType.AmigaVolumeEntryWriter)]
    [InlineData(EntryWriterType.FileSystemEntryWriter)]
    [InlineData(EntryWriterType.DirectoryEntryWriter)]
    public async Task When_RootPathComponentsExistCreatingDirectory_Then_DirectoryIsCreated(
        EntryWriterType entryWriterType)
    {
        var path = string.Concat(Guid.NewGuid(), entryWriterType != EntryWriterType.DirectoryEntryWriter
            ? ".vhd" : string.Empty);

        try
        {
            using var testCommandHelper = new TestCommandHelper();

            await EntryWriterTestHelper.CreateMedia(entryWriterType, testCommandHelper, path);

            // arrange - create entry writer
            var entryWriter = await EntryWriterTestHelper.CreateEntryWriter(entryWriterType, testCommandHelper, path,
                rootPathComponents, false);

            // arrange - initialize the writer
            var initializeResult = await entryWriter.Initialize();
            Assert.True(initializeResult.IsSuccess);

            // arrange - create entry for dir2
            var entry = new Entry
            {
                Name = "dir2",
                Size = 0,
                Date = DateTime.Now,
                Type = EntryType.Dir,
                RawPath = "dir2",
                FormattedName = "dir2",
                RelativePathComponents = ["dir2"],
                FullPathComponents = ["dir1", "dir2"]
            };

            // act - create directory
            var createDirectoryResult = await entryWriter.CreateDirectory(entry, entry.RelativePathComponents,
                true, false);

            // assert - create directory result is success
            Assert.True(createDirectoryResult.IsSuccess);
        }
        finally
        {
            TestHelper.DeletePaths(path);
        }
    }
}