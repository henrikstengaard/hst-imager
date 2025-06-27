using Hst.Amiga.DataTypes.UaeFsDbs;
using Hst.Amiga.DataTypes.UaeMetafiles;
using Hst.Amiga.FileSystems;
using Hst.Core.Extensions;
using Hst.Imager.Core.Commands;

namespace Hst.Imager.Core.UaeMetadatas;

using System;
using System.IO;
using System.Threading.Tasks;

public static class UaeMetadataHelper
{
    private static readonly bool isWindowsOperatingSystem = OperatingSystem.IsWindows();

    public static bool RequiresUaeMetadataProperties(int protectionBits, string comment) =>
        protectionBits != 0 || !string.IsNullOrEmpty(comment);

    public static bool RequiresUaeMetadataFileName(UaeMetadata uaeMetadata, string fileName)
    {
        if (isWindowsOperatingSystem && Regexs.WindowsReservedNamesRegex.IsMatch(fileName))
        {
            return true;
        }

        return uaeMetadata switch
        {
            UaeMetadata.UaeFsDb => UaeFsDbNodeHelper.HasSpecialFilenameChars(fileName),
            UaeMetadata.UaeMetafile => UaeMetafileHelper.HasSpecialFilenameChars(fileName),
            _ => HasInvalidFilenameChars(fileName)
        };
    }

    public static bool HasInvalidFilenameChars(string fileName)
    {
        if (fileName.Equals(".") || fileName.Equals(".."))
        {
            return true;
        }

        return Regexs.InvalidFilenameCharsRegex.IsMatch(fileName) ||
        isWindowsOperatingSystem && Regexs.WindowsReservedNamesRegex.IsMatch(fileName);
    }

    public static string CreateNormalFilename(string fileName)
    {
        if (fileName.Equals(".") || fileName.Equals(".."))
        {
            return Regexs.InvalidFilenameCharsRegex.Replace(fileName, "_");
        }

        if (isWindowsOperatingSystem && Regexs.WindowsReservedNamesRegex.IsMatch(fileName))
        {
            return $"_{fileName}";
        }

        return Regexs.InvalidFilenameCharsRegex.Replace(fileName, "_");
    }

    public static string CreateUaeMetadataFileName(UaeMetadata uaeMetadata, string dirPath, string fileName)
    {
        return uaeMetadata switch
        {
            UaeMetadata.UaeFsDb => UaeFsDbNodeHelper.CreateUniqueNormalName(dirPath,
                UaeFsDbNodeHelper.MakeSafeFilename(fileName)),
            UaeMetadata.UaeMetafile => Regexs.WindowsReservedNamesRegex.IsMatch(fileName)
                ? UaeMetafileHelper.EncodeFilename(fileName)
                : UaeMetafileHelper.EncodeFilenameSpecialChars(fileName),
            _ => CreateNormalFilename(fileName)
        };
    }

    public static async Task WriteUaeMetadata(UaeMetadata uaeMetadata, string dirPath, string amigaName, string normalName, int protectionBits, DateTime date, string comment)
    {
        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }

        switch (uaeMetadata)
        {
            case UaeMetadata.UaeFsDb:
                await WriteUaeFsDb(dirPath, amigaName, normalName, protectionBits, comment);
                break;
            case UaeMetadata.UaeMetafile:
                await WriteUaeMetafile(dirPath, normalName, protectionBits, date, comment);
                break;
        }
    }

    public static async Task WriteUaeFsDb(string dirPath, string amigaName, string normalName, int protectionBits, string comment)
    {
        var uaeFsDbPath = Path.Combine(dirPath, "_UAEFSDB.___");

        await using var stream = new FileStream(uaeFsDbPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

        var hasNode = false;
        UaeFsDbNode node = null;

        while (stream.Length >= Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbNodeVersion1Size && stream.Position < stream.Length)
        {
            var position = stream.Position;
            var nodeBytes = await stream.ReadBytes(Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbNodeVersion1Size);

            node = UaeFsDbReader.ReadFromBytes(nodeBytes);

            if (!node.AmigaName.Equals(amigaName))
            {
                continue;
            }

            // seek back to position to overwrite node
            hasNode = true;
            stream.Seek(position, SeekOrigin.Begin);
            break;
        }

        if (hasNode && node != null && node.NormalName.Equals(normalName) && node.Mode == protectionBits && node.Comment.Equals(comment))
        {
            return;
        }

        var newNode = new UaeFsDbNode
        {
            Version = UaeFsDbNode.NodeVersion.Version1,
            Mode = (uint)protectionBits,
            Comment = comment,
            NormalName = normalName,
            AmigaName = amigaName
        };

        var newNodeBytes = UaeFsDbWriter.Build(newNode);

        if (!hasNode)
        {
            stream.Seek(0, SeekOrigin.End);
        }

        await stream.WriteAsync(newNodeBytes, 0, newNodeBytes.Length);
    }

    public static async Task WriteUaeMetafile(string dirPath, string normalName, int protectionBits, DateTime date, string comment)
    {
        var uaeMetafile = new UaeMetafile
        {
            ProtectionBits = EntryFormatter.FormatProtectionBits((ProtectionBits)(protectionBits ^ 0xf)).ToLower(), // mask away "RWED" protection bits
            Date = date,
            Comment = comment
        };

        var uaeMetafileBytes = UaeMetafileWriter.Build(uaeMetafile);

        var uaeMetafilePath = Path.Combine(dirPath, string.Concat(Path.GetFileNameWithoutExtension(normalName),
            Amiga.DataTypes.UaeMetafiles.Constants.UaeMetafileExtension));

        await File.WriteAllBytesAsync(uaeMetafilePath, uaeMetafileBytes);
    }

    /// <summary>
    /// Entry iterator supports uae metadata.
    /// </summary>
    /// <param name="entryIterator">Entry iterator.</param>
    /// <returns>True, if entry iterator supports reading uae metadata.</returns>
    public static bool EntryIteratorSupportsUaeMetadata(IEntryIterator entryIterator)
    {
        return entryIterator switch
        {
            AmigaVolumeEntryIterator _ or DirectoryEntryIterator _ or LhaArchiveEntryIterator _ or LzxArchiveEntryIterator _ => true,
            _ => false,
        };
    }

    /// <summary>
    /// Entry writer supports uae metadata
    /// </summary>
    /// <param name="entryWriter">Entry writer.</param>
    /// <returns>True, if entry writer supports writing uae metadata.</returns>
    public static bool EntryWriterSupportsUaeMetadata(IEntryWriter entryWriter)
    {
        return entryWriter switch
        {
            AmigaVolumeEntryWriter _ or DirectoryEntryWriter _ => true,
            _ => false,
        };
    }
}