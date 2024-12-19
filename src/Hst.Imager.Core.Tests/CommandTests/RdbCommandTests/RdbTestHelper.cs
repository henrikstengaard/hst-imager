using Hst.Amiga.FileSystems;
using Hst.Amiga.FileSystems.Pfs3;
using Hst.Amiga.RigidDiskBlocks;
using Hst.Imager.Core.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Hst.Imager.Core.Tests.CommandTests.RdbCommandTests
{
    public static class RdbTestHelper
    {
        private static async Task<Pfs3Volume> MountPfs3Volume(Stream stream)
        {
            stream.Position = 0;
            var rigidDiskBlock = await RigidDiskBlockReader.Read(stream);

            var partitionBlock = rigidDiskBlock.PartitionBlocks.First();

            return await Pfs3Volume.Mount(stream, partitionBlock);
        }

        public static async Task<IEnumerable<Entry>> ListPfs3Entries(TestCommandHelper testCommandHelper, string path, string[] subDirectories)
        {
            var mediaResult = await testCommandHelper.GetWritableFileMedia(path);
            if (mediaResult.IsFaulted)
            {
                throw new IOException(mediaResult.Error.ToString());
            }

            using var media = mediaResult.Value;
            var stream = media is DiskMedia diskMedia ? diskMedia.Disk.Content : media.Stream;

            await using var pfs3Volume = await MountPfs3Volume(stream);

            foreach (var subDirectory in subDirectories)
            {
                await pfs3Volume.ChangeDirectory(subDirectory);
            }

            return await pfs3Volume.ListEntries();
        }
    }
}
