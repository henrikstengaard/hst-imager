using Hst.Imager.Core.Commands;
using Hst.Imager.Core.PathComponents;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.EntryIteratorFunctionsTests
{
    public class GivenEntryIteratorFunctionsCreateEntries
    {
        [Fact]
        public void When_CreateEntriesWithNoRootPathComponents_Then_OneEntryIsCreated()
        {
            // arrange
            var mediaPath = MediaPath.ForwardSlashMediaPath;
            const bool recursive = false;
            var rootPathComponents = Array.Empty<string>();
            var pathComponentMatcher = new PathComponentMatcher(rootPathComponents, recursive);
            var entryPath = "dir1/file2.txt";
            var rawPath = entryPath;
            var isDir = false;
            var date = new DateTime(2024, 2, 1, 12, 0, 0, 0, DateTimeKind.Local);
            var size = 0;
            var fileAttributes = "ATTRIBUTES";
            var fileProperties = new Dictionary<string, string>();
            var dirAttributes = "ATTRIBUTES";

            // act
            var entries = EntryIteratorFunctions.CreateEntries(
                mediaPath,
                pathComponentMatcher,
                rootPathComponents,
                recursive,
                entryPath,
                rawPath,
                isDir,
                date,
                size,
                fileAttributes,
                fileProperties,
                dirAttributes).ToArray();

            // assert - 1 entry is created
            Assert.Single(entries);

            // assert - directory is created
            var expectedDirNames = new[]
            {
                "dir1",
            };
            var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
            Assert.Equal(expectedDirNames, dirNames);
        }

        [Fact]
        public void When_CreateEntriesWithNoRootPathComponentsRecursive_Then_TwoEntriesAreCreated()
        {
            // arrange
            var mediaPath = MediaPath.ForwardSlashMediaPath;
            const bool recursive = true;
            var rootPathComponents = Array.Empty<string>();
            var pathComponentMatcher = new PathComponentMatcher(rootPathComponents, recursive);
            var entryPath = "dir1/file2.txt";
            var rawPath = entryPath;
            var isDir = false;
            var date = new DateTime(2024, 2, 1, 12, 0, 0, 0, DateTimeKind.Local);
            var size = 0;
            var fileAttributes = "ATTRIBUTES";
            var fileProperties = new Dictionary<string, string>();
            var dirAttributes = "ATTRIBUTES";

            // act
            var entries = EntryIteratorFunctions.CreateEntries(
                mediaPath,
                pathComponentMatcher,
                rootPathComponents,
                recursive,
                entryPath,
                rawPath,
                isDir,
                date,
                size,
                fileAttributes,
                fileProperties,
                dirAttributes).ToArray();

            // assert - 2 entries are created
            Assert.Equal(2, entries.Length);

            // assert - directory is created
            var expectedDirNames = new[]
            {
                "dir1",
            };
            var dirNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.Dir).Select(x => x.Name).ToList();
            Assert.Equal(expectedDirNames, dirNames);

            // assert - file is created
            var expectedFileNames = new[]
            {
                "dir1/file2.txt"
            };
            var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
            Assert.Equal(expectedFileNames, fileNames);
        }

        [Fact]
        public void When_CreateEntriesWithOneRootPathComponent_Then_OneEntriesIsCreated()
        {
            // arrange
            var mediaPath = MediaPath.ForwardSlashMediaPath;
            const bool recursive = false;
            var rootPathComponents = new[] { "dir1" };
            var pathComponentMatcher = new PathComponentMatcher(rootPathComponents, recursive);
            var entryPath = "dir1/file2.txt";
            var rawPath = entryPath;
            var isDir = false;
            var date = new DateTime(2024, 2, 1, 12, 0, 0, 0, DateTimeKind.Local);
            var size = 0;
            var fileAttributes = "ATTRIBUTES";
            var fileProperties = new Dictionary<string, string>();
            var dirAttributes = "ATTRIBUTES";

            // act
            var entries = EntryIteratorFunctions.CreateEntries(
                mediaPath,
                pathComponentMatcher,
                rootPathComponents,
                recursive,
                entryPath,
                rawPath,
                isDir,
                date,
                size,
                fileAttributes,
                fileProperties,
                dirAttributes).ToArray();

            // assert - 1 entry is created
            Assert.Single(entries);

            // assert - file is created
            var expectedFileNames = new[]
            {
                "file2.txt"
            };
            var fileNames = entries.Where(x => x.Type == Models.FileSystems.EntryType.File).Select(x => x.Name).ToList();
            Assert.Equal(expectedFileNames, fileNames);
        }
    }
}