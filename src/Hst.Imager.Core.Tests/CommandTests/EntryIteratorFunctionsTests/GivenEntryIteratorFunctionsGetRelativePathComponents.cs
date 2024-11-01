using Hst.Imager.Core.Commands.PathComponents;
using System.Linq;
using Xunit;

namespace Hst.Imager.Core.Tests.CommandTests.EntryIteratorFunctionsTests
{
    public class GivenEntryIteratorFunctionsGetRelativePathComponents
    {
        [Fact]
        public void When_FirstPathComponentsAreEqualOfTwoFullPathComponents_Then_LastPathComponentIsReturned()
        {
            // arrange
            var rootPathComponents = new[] { "dir1" };
            var fullPathComponents = new[] { "dir1", "file2.txt" };

            // act
            var relativePathComponents = EntryIteratorFunctions.GetRelativePathComponents(
                rootPathComponents, fullPathComponents).ToArray();

            // assert
            Assert.Equal(new[] { "file2.txt" }, relativePathComponents);
        }

        [Fact]
        public void When_FirstPathComponentsAreEqualOfThreeFullPathComponents_Then_LastPathComponentsAreReturned()
        {
            // arrange
            var rootPathComponents = new[] { "dir1" };
            var fullPathComponents = new[] { "dir1", "dir2", "file3.txt" };

            // act
            var relativePathComponents = EntryIteratorFunctions.GetRelativePathComponents(
                rootPathComponents, fullPathComponents).ToArray();

            // assert
            Assert.Equal(new[] { "dir2", "file3.txt" }, relativePathComponents);
        }

        [Fact]
        public void When_FirstTwoRootAndFullPathComponentsAreEqual_Then_LastPathComponentIsReturned()
        {
            // arrange
            var rootPathComponents = new[] { "dir1", "file2.txt" };
            var fullPathComponents = new[] { "dir1", "file2.txt" };

            // act
            var relativePathComponents = EntryIteratorFunctions.GetRelativePathComponents(
                rootPathComponents, fullPathComponents).ToArray();

            // assert
            Assert.Equal(new[] { "file2.txt" }, relativePathComponents);
        }
    }
}