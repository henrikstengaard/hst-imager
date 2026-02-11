using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Hst.Amiga.DataTypes.UaeFsDbs;

namespace Hst.Imager.Core.Tests;

public static class UaeMetadataTestHelper
{
    public static async Task<IEnumerable<UaeFsDbNode>> ReadUaeFsDbNodes(string uaeFsDbPath)
    {
        var uaeFsDbBytes = await File.ReadAllBytesAsync(uaeFsDbPath);
        var uaeFsDbNodes = new List<UaeFsDbNode>();
        var offset = 0;
        while (offset + Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbNodeVersion1Size <= uaeFsDbBytes.Length)
        {
            uaeFsDbNodes.Add(UaeFsDbReader.ReadFromBytes(uaeFsDbBytes, offset));
            offset += Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbNodeVersion1Size;
        }

        return uaeFsDbNodes;
    }
}