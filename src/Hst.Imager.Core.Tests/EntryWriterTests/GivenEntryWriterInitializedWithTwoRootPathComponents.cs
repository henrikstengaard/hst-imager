using System;
using System.Threading.Tasks;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Models.FileSystems;
using Xunit;

namespace Hst.Imager.Core.Tests.EntryWriterTests;

public class GivenEntryWriterInitializedWithTwoRootPathComponents
{
    private readonly string[] rootPathComponents = ["dir1", "dir2"];

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

            // arrange - create directory root path components
            await EntryWriterTestHelper.CreateDirectory(entryWriterType, testCommandHelper, path, rootPathComponents);

            // arrange - create entry writer
            var entryWriter = await EntryWriterTestHelper.CreateEntryWriter(entryWriterType, testCommandHelper, path,
                rootPathComponents, false);

            // arrange - initialize the writer
            var initializeResult = await entryWriter.Initialize();
            Assert.True(initializeResult.IsSuccess);

            // arrange - create entry for dir3
            var entry = new Entry
            {
                Name = "dir3",
                Size = 0,
                Date = DateTime.Now,
                Type = EntryType.Dir,
                RawPath = "dir3",
                FormattedName = "dir3",
                RelativePathComponents = ["dir3"],
                FullPathComponents = ["dir1", "dir2", "dir3"]
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

    [Theory]
    [InlineData(EntryWriterType.AmigaVolumeEntryWriter)]
    [InlineData(EntryWriterType.FileSystemEntryWriter)]
    [InlineData(EntryWriterType.DirectoryEntryWriter)]
    public async Task When_RootPathComponentsDoesntExistInitializing_Then_ErrorIsReturned(
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

            // act - initialize the writer
            var initializeResult = await entryWriter.Initialize();

            // assert - error is returned because dir1, dir2 does not exist
            Assert.True(initializeResult.IsFaulted);
            Assert.False(initializeResult.IsSuccess);
            Assert.IsType<PathNotFoundError>(initializeResult.Error);
        }
        finally
        {
            TestHelper.DeletePaths(path);
        }
    }
}