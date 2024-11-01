using Hst.Imager.Core.PathComponents;
using System.Linq;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.EntryIteratorFunctionsTests
{
    public class GivenEntryIteratorFunctionsGetDirEntries
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void When_OneRelativePathComponent_Then_NoDirEntriesAreReturned(bool recursive)
        {
            // arrange
            var mediaPath = MediaPath.ForwardSlashMediaPath;
            var relativePathComponents = new[] { "file1.txt" };
            var attributes = "ATTRIBUTES";

            // act
            var dirEntries = EntryIteratorFunctions.GetDirEntries(mediaPath, relativePathComponents,
                attributes, recursive).ToArray();

            // assert
            Assert.Empty(dirEntries);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void When_TwoRelativePathComponents_Then_OneDirEntryIsReturned(bool recursive)
        {
            // arrange
            var mediaPath = MediaPath.ForwardSlashMediaPath;
            var relativePathComponents = new[] { "dir1", "file2.txt" };
            var attributes = "ATTRIBUTES";

            // act
            var dirEntries = EntryIteratorFunctions.GetDirEntries(mediaPath, relativePathComponents,
                attributes, recursive).ToArray();

            // assert
            Assert.Single(dirEntries);
            var dirEntry = dirEntries.First();
            Assert.Equal("dir1", dirEntry.Name);
            Assert.Equal(Models.FileSystems.EntryType.Dir, dirEntry.Type);
            Assert.Equal(0, dirEntry.Size);
            Assert.Equal(new[] { "dir1" }, dirEntry.FullPathComponents);
            Assert.Equal(new[] { "dir1" }, dirEntry.RelativePathComponents);
        }

        [Fact]
        public void When_ThreeRelativePathComponents_Then_OneDirEntryIsReturned()
        {
            // arrange
            var mediaPath = MediaPath.ForwardSlashMediaPath;
            var relativePathComponents = new[] { "dir1", "dir2", "file3.txt" };
            var attributes = "ATTRIBUTES";
            const bool recursive = false;

            // act
            var dirEntries = EntryIteratorFunctions.GetDirEntries(mediaPath, relativePathComponents,
                attributes, recursive).ToArray();

            // assert
            Assert.Single(dirEntries);
            var dirEntry = dirEntries.First();
            Assert.Equal("dir1", dirEntry.Name);
            Assert.Equal(Models.FileSystems.EntryType.Dir, dirEntry.Type);
            Assert.Equal(0, dirEntry.Size);
            Assert.Equal(new[] { "dir1" }, dirEntry.FullPathComponents);
            Assert.Equal(new[] { "dir1" }, dirEntry.RelativePathComponents);
        }

        [Fact]
        public void When_ThreeRelativePathComponentsRecursive_Then_TwoDirEntriesAreReturned()
        {
            // arrange
            var mediaPath = MediaPath.ForwardSlashMediaPath;
            var relativePathComponents = new[] { "dir1", "dir2", "file3.txt" };
            var attributes = "ATTRIBUTES";
            const bool recursive = true;

            // act
            var dirEntries = EntryIteratorFunctions.GetDirEntries(mediaPath, relativePathComponents,
                attributes, recursive).ToArray();

            // assert - 2 dir entries are returned
            Assert.Equal(2, dirEntries.Length);

            // assert - dir1 entry
            var dir1Entry = dirEntries.FirstOrDefault(x => x.Name == "dir1");
            Assert.Equal(Models.FileSystems.EntryType.Dir, dir1Entry.Type);
            Assert.Equal(0, dir1Entry.Size);
            Assert.Equal(new[] { "dir1" }, dir1Entry.FullPathComponents);
            Assert.Equal(new[] { "dir1" }, dir1Entry.RelativePathComponents);

            // assert - dir2 entry
            var dir2Entry = dirEntries.FirstOrDefault(x => x.Name == "dir1/dir2");
            Assert.Equal(Models.FileSystems.EntryType.Dir, dir2Entry.Type);
            Assert.Equal(0, dir2Entry.Size);
            Assert.Equal(new[] { "dir1", "dir2" }, dir2Entry.FullPathComponents);
            Assert.Equal(new[] { "dir1", "dir2" }, dir2Entry.RelativePathComponents);
        }
    }
}