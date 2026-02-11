using System;
using Hst.Amiga.DataTypes.UaeFsDbs;
using Hst.Amiga.DataTypes.UaeMetafiles;
using Hst.Amiga.FileSystems;

namespace Hst.Imager.Core.UaeMetadatas;

public class UaeMetadataNode
{
    public string AmigaName { get; set; }
    public string NormalName { get; set; }
    public int ProtectionBits { get; set; }
    public DateTime Date { get; set; }
    public string Comment { get; set; }

    public static UaeMetadataNode FromUaeFsDbNode(UaeFsDbNode uaeFsDbNode)
    {
        return new UaeMetadataNode
        {
            AmigaName = uaeFsDbNode.Version == UaeFsDbNode.NodeVersion.Version2
                ? uaeFsDbNode.AmigaNameUnicode
                : uaeFsDbNode.AmigaName,
            NormalName = uaeFsDbNode.Version == UaeFsDbNode.NodeVersion.Version2
                ? uaeFsDbNode.NormalNameUnicode
                : uaeFsDbNode.NormalName,
            ProtectionBits = (int)uaeFsDbNode.Mode,
            Comment = uaeFsDbNode.Comment
        };
    }

    public static UaeMetadataNode FromUaeMetafile(UaeMetafile uaeMetafile, string amigaName, string normalName)
    {
        return new UaeMetadataNode
        {
            AmigaName = amigaName,
            NormalName = normalName,
            ProtectionBits =
                (int)ProtectionBitsConverter.ParseProtectionBits(uaeMetafile.ProtectionBits) ^
                0xf,
            Comment = uaeMetafile.Comment,
            Date = uaeMetafile.Date
        };
    }
}