using System;
using Xunit;
using Hst.Imager.Core.Commands;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hst.Imager.Core.Tests.CommandTests
{
    public class GivenFileSystemVersionComparer
    {
        [Fact]
        public void When_SortingFileSystemsByVersionInAscendingOrder_Then_FileSystemsAreSorted()
        {
            // arrange - file systems where first has highest version
            var fileSystems = new List<Tuple<string, byte[]>>
            {
                new Tuple<string, byte[]>("FastFileSystemDos7", TestHelper.FastFileSystemDos7Bytes),
                new Tuple<string, byte[]>("FastFileSystemDos3", TestHelper.FastFileSystemDos3Bytes)
            };

            // act
            var sorted = fileSystems.Order(new FileSystemVersionComparer()).ToList();

            // assert
            Assert.Equal(2, sorted.Count);
            Assert.Equal("FastFileSystemDos3", sorted[0].Item1);
            Assert.Equal("FastFileSystemDos7", sorted[1].Item1);
        }

        [Fact]
        public void When_SortingFileSystemsByVersionInAscendingOrderWithoutVersionInFirst_Then_FileSystemsAreSorted()
        {
            // arrange - file systems where first is without version
            var fileSystems = new List<Tuple<string, byte[]>>
            {
                new Tuple<string, byte[]>("FastFileSystemDos7", []),
                new Tuple<string, byte[]>("FastFileSystemDos3", TestHelper.FastFileSystemDos3Bytes)
            };

            // act
            var sorted = fileSystems.Order(new FileSystemVersionComparer()).ToList();

            // assert
            Assert.Equal(2, sorted.Count);
            Assert.Equal("FastFileSystemDos7", sorted[0].Item1);
            Assert.Equal("FastFileSystemDos3", sorted[1].Item1);
        }

        [Fact]
        public void When_SortingFileSystemsByVersionInAscendingOrderWithoutVersionInLast_Then_FileSystemsAreSorted()
        {
            // arrange - file systems where last is without version
            var fileSystems = new List<Tuple<string, byte[]>>
            {
                new Tuple<string, byte[]>("FastFileSystemDos7", TestHelper.FastFileSystemDos7Bytes),
                new Tuple<string, byte[]>("FastFileSystemDos3", [])
            };

            // act
            var sorted = fileSystems.Order(new FileSystemVersionComparer()).ToList();

            // assert
            Assert.Equal(2, sorted.Count);
            Assert.Equal("FastFileSystemDos3", sorted[0].Item1);
            Assert.Equal("FastFileSystemDos7", sorted[1].Item1);
        }

        [Fact]
        public void When_SortingFileSystemsByVersionInDescendingOrder_Then_FileSystemsAreSorted()
        {
            // arrange - file systems where last has highest version
            var fileSystems = new List<Tuple<string, byte[]>>
            {
                new Tuple<string, byte[]>("FastFileSystemDos3", TestHelper.FastFileSystemDos3Bytes),
                new Tuple<string, byte[]>("FastFileSystemDos7", TestHelper.FastFileSystemDos7Bytes)
            };

            // act
            var sorted = fileSystems.OrderDescending(new FileSystemVersionComparer()).ToList();

            // assert
            Assert.Equal(2, sorted.Count);
            Assert.Equal("FastFileSystemDos7", sorted[0].Item1);
            Assert.Equal("FastFileSystemDos3", sorted[1].Item1);
        }

        [Fact]
        public void When_SortingFileSystemsByVersionInDescendingOrderWithoutVersionInFirst_Then_FileSystemsAreSorted()
        {
            // arrange - file systems where first is without version
            var fileSystems = new List<Tuple<string, byte[]>>
            {
                new Tuple<string, byte[]>("FastFileSystemDos7", []),
                new Tuple<string, byte[]>("FastFileSystemDos3", TestHelper.FastFileSystemDos3Bytes)
            };

            // act
            var sorted = fileSystems.OrderDescending(new FileSystemVersionComparer()).ToList();

            // assert
            Assert.Equal(2, sorted.Count);
            Assert.Equal("FastFileSystemDos3", sorted[0].Item1);
            Assert.Equal("FastFileSystemDos7", sorted[1].Item1);
        }

        [Fact]
        public void When_SortingFileSystemsByVersionInDescendingOrderWithoutVersionInLast_Then_FileSystemsAreSorted()
        {
            // arrange - file systems where last is without version
            var fileSystems = new List<Tuple<string, byte[]>>
            {
                new Tuple<string, byte[]>("FastFileSystemDos7", TestHelper.FastFileSystemDos7Bytes),
                new Tuple<string, byte[]>("FastFileSystemDos3", [])
            };

            // act
            var sorted = fileSystems.OrderDescending(new FileSystemVersionComparer()).ToList();

            // assert
            Assert.Equal(2, sorted.Count);
            Assert.Equal("FastFileSystemDos7", sorted[0].Item1);
            Assert.Equal("FastFileSystemDos3", sorted[1].Item1);
        }

        [Fact]
        public void When_SortingFileSystemsByVersionInAscendingOrderWithVersionIsIdentical_Then_FileSystemsAreSortedByRevision()
        {
            // arrange - file systems where last is without version
            var fileSystems = new List<Tuple<string, byte[]>>
            {
                new Tuple<string, byte[]>("FastFileSystemDos2", Encoding.ASCII.GetBytes("$VER: FastFileSystem 1.2 (02/02/22)")),
                new Tuple<string, byte[]>("FastFileSystemDos1", Encoding.ASCII.GetBytes("$VER: FastFileSystem 1.1 (01/01/22)"))
            };

            // act
            var sorted = fileSystems.Order(new FileSystemVersionComparer()).ToList();

            // assert
            Assert.Equal(2, sorted.Count);
            Assert.Equal("FastFileSystemDos1", sorted[0].Item1);
            Assert.Equal("FastFileSystemDos2", sorted[1].Item1);
        }

        [Fact]
        public void When_SortingFileSystemsByVersionInDescendingOrderWithVersionIsIdentical_Then_FileSystemsAreSortedByRevision()
        {
            // arrange - file systems where last is without version
            var fileSystems = new List<Tuple<string, byte[]>>
            {
                new Tuple<string, byte[]>("FastFileSystemDos1", Encoding.ASCII.GetBytes("$VER: FastFileSystem 1.1 (01/01/22)")),
                new Tuple<string, byte[]>("FastFileSystemDos2", Encoding.ASCII.GetBytes("$VER: FastFileSystem 1.2 (02/02/22)"))
            };

            // act
            var sorted = fileSystems.OrderDescending(new FileSystemVersionComparer()).ToList();

            // assert
            Assert.Equal(2, sorted.Count);
            Assert.Equal("FastFileSystemDos2", sorted[0].Item1);
            Assert.Equal("FastFileSystemDos1", sorted[1].Item1);
        }
    }
}