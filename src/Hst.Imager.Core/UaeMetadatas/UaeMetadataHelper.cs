using System.Collections.Generic;
using System.Linq;
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
    private static readonly bool IsWindowsOperatingSystem = OperatingSystem.IsWindows();

    public static bool RequiresUaeMetadataProperties(int? protectionBits, string comment) =>
        protectionBits.HasValue && protectionBits != 0 || !string.IsNullOrEmpty(comment);

    private static bool HasInvalidFilenameChars(string fileName)
    {
        if (fileName.Equals(".") || fileName.Equals(".."))
        {
            return true;
        }

        return Regexs.InvalidFilenameCharsRegex.IsMatch(fileName) ||
               IsWindowsOperatingSystem && HasWindowsReservedNames(fileName);
    }

    private static bool HasWindowsReservedNames(string fileName)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName) ?? string.Empty;

        return Regexs.WindowsReservedNamesRegex.IsMatch(fileName) ||
               Regexs.WindowsReservedNamesRegex.IsMatch(fileNameWithoutExtension);
    }

    public static bool RequiresUaeMetadataFileName(UaeMetadata uaeMetadata, string fileName)
    {
        if (IsWindowsOperatingSystem && HasWindowsReservedNames(fileName))
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

    public static string CreateNormalFilename(string fileName)
    {
        if (fileName.Equals(".") || fileName.Equals(".."))
        {
            return Regexs.InvalidFilenameCharsRegex.Replace(fileName, "_");
        }

        if (IsWindowsOperatingSystem && HasWindowsReservedNames(fileName))
        {
            return $"_{fileName}";
        }

        return Regexs.InvalidFilenameCharsRegex.Replace(fileName, "_");
    }

    public static string CreateUaeMetadataFileName(UaeMetadata uaeMetadata, string dirPath, string fileName)
    {
        if (!RequiresUaeMetadataFileName(uaeMetadata, fileName))
        {
            return fileName;
        }
        
        return uaeMetadata switch
        {
            UaeMetadata.UaeFsDb => UaeFsDbNodeHelper.CreateUniqueNormalName(dirPath,
                UaeFsDbNodeHelper.MakeSafeFilename(fileName)),
            UaeMetadata.UaeMetafile => HasWindowsReservedNames(fileName)
                ? UaeMetafileHelper.EncodeFilename(fileName)
                : UaeMetafileHelper.EncodeFilenameSpecialChars(fileName),
            _ => CreateNormalFilename(fileName)
        };
    }

    public static async Task<IEnumerable<DirectoryEntryIterator.UaeMetadataNode>> ReadUaeMetadataNodes(
        UaeMetadata uaeMetadata, string path, string amigaNameFilter = null)
    {
        switch (uaeMetadata)
        {
            case UaeMetadata.UaeFsDb:
                return await ReadUaeFsDbFile(path, amigaNameFilter);
            case UaeMetadata.UaeMetafile:
                return ReadUaeMetafiles(path, amigaNameFilter);
            case UaeMetadata.None:
            default:
                return new List<DirectoryEntryIterator.UaeMetadataNode>();
        }
    }

    private static async Task<IEnumerable<DirectoryEntryIterator.UaeMetadataNode>> ReadUaeFsDbFile(string path,
        string amigaNameFilter)
    {
        var uaeFsDbFilePath = Path.Combine(path, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);

        if (File.Exists(uaeFsDbFilePath))
        {
            return await ReadUaeFsDbFileVersion1(path, amigaNameFilter);
        }

        return await ReadUaeFsDbFileVersion2(path, amigaNameFilter);
    }
    
    private static async Task<IEnumerable<DirectoryEntryIterator.UaeMetadataNode>> ReadUaeFsDbFileVersion1(
        string path, string amigaNameFilter = null)
    {
        var uaeFsDbFilePath = Path.Combine(path, Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName);

        if (!File.Exists(uaeFsDbFilePath))
        {
            return new List<DirectoryEntryIterator.UaeMetadataNode>();
        }

        var uaeFsDbNodes = await UaeFsDbReader.ReadFromFile(uaeFsDbFilePath);

        var uaeMetadataNodes = new List<DirectoryEntryIterator.UaeMetadataNode>();

        if (!string.IsNullOrEmpty(amigaNameFilter))
        {
            uaeFsDbNodes = uaeFsDbNodes.Where(x => x.AmigaName.Equals(amigaNameFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        
        foreach (var uaeFsDbNode in uaeFsDbNodes)
        {
            var uaeMetadataNode = DirectoryEntryIterator.UaeMetadataNode.FromUaeFsDbNode(uaeFsDbNode);

            var filePath = Path.Combine(path, uaeFsDbNode.NormalName);

            uaeMetadataNode.Date = File.Exists(filePath)
                ? new FileInfo(filePath).LastWriteTime
                : DateTime.Now;

            uaeMetadataNodes.Add(uaeMetadataNode);
        }

        return uaeMetadataNodes;
    }

    private static async Task<IEnumerable<DirectoryEntryIterator.UaeMetadataNode>> ReadUaeFsDbFileVersion2(
        string path, string amigaNameFilter = null)
    {
        var uaeMetadataNodes = new List<DirectoryEntryIterator.UaeMetadataNode>();

        if (!Directory.Exists(path))
        {
            return uaeMetadataNodes;
        }
        
        var dirInfo = new DirectoryInfo(path);

        var uaeFsDbAlternativeStreamPaths = dirInfo.GetDirectories().Select(x => string.Concat(x.FullName, ":", Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName))
            .Concat(dirInfo.GetFiles().Select(x => string.Concat(x.FullName, ":", Amiga.DataTypes.UaeFsDbs.Constants.UaeFsDbFileName)))
            .ToList();

        foreach (var uaeFsDbAlternativeStreamPath in uaeFsDbAlternativeStreamPaths)
        {
            if (!File.Exists(uaeFsDbAlternativeStreamPath))
            {
                continue;
            }

            var uaeFsDbNodes = await UaeFsDbReader.ReadFromFile(uaeFsDbAlternativeStreamPath);

            uaeMetadataNodes.AddRange(uaeFsDbNodes.Select(x => DirectoryEntryIterator.UaeMetadataNode.FromUaeFsDbNode(x)));
        }
        
        if (!string.IsNullOrEmpty(amigaNameFilter))
        {
            uaeMetadataNodes = uaeMetadataNodes.Where(x => x.AmigaName.Equals(amigaNameFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return uaeMetadataNodes;
    }
    
    private static IEnumerable<DirectoryEntryIterator.UaeMetadataNode> ReadUaeMetafiles(string path,
        string amigaNameFilter = null)
    {
        var uaeMetadataNodes = new List<DirectoryEntryIterator.UaeMetadataNode>();
        
        if (!Directory.Exists(path))
        {
            return uaeMetadataNodes;
        }
        
        var dirInfo = new DirectoryInfo(path);

        foreach (var file in dirInfo.GetFiles($"*{Amiga.DataTypes.UaeMetafiles.Constants.UaeMetafileExtension}", SearchOption.TopDirectoryOnly))
        {
            var uaeMetafile = UaeMetafileReader.Read(File.ReadAllBytes(file.FullName));

            var name = file.Name.Substring(0, file.Name.Length - Amiga.DataTypes.UaeMetafiles.Constants.UaeMetafileExtension.Length);
            var amigaName = UaeMetafileHelper.DecodeFilename(name);

            if (!string.IsNullOrWhiteSpace(amigaNameFilter) &&
                !amigaName.Equals(amigaNameFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            
            var uaeMetadataNode = DirectoryEntryIterator.UaeMetadataNode.FromUaeMetafile(uaeMetafile, amigaName, name);

            uaeMetadataNodes.Add(uaeMetadataNode);
        }

        return uaeMetadataNodes;
    }

    public static bool IsChanged(string normalName1, int? protectionBits1, DateTime? date1, string comment1,
        string normalName2, int? protectionBits2, DateTime? date2, string comment2)
    {
        if (protectionBits1.HasValue && protectionBits2.HasValue &&
            protectionBits1 != protectionBits2)
        {
            return true;
        }
        
        if (date1.HasValue && date2.HasValue &&
            date1 != date2)
        {
            return true;
        }

        return !(comment1 ?? string.Empty).Equals(comment2 ?? string.Empty);
    }
    
    public static async Task WriteUaeMetadata(UaeMetadata uaeMetadata, string dirPath, string amigaName, 
        string normalName, int? protectionBits = null, DateTime? date = null, string comment = null)
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

    public static async Task WriteUaeFsDb(string dirPath, string amigaName, string normalName, int? protectionBits, string comment)
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

        var nodeUpdated = false;

        // node not found, create new one
        if (!hasNode)
        {
            node = new UaeFsDbNode
            {
                Version = UaeFsDbNode.NodeVersion.Version1,
                Mode = protectionBits.HasValue ? (uint)protectionBits : 0U,
                Comment = string.IsNullOrWhiteSpace(comment) ? string.Empty : comment,
                NormalName = normalName,
                AmigaName = amigaName
            };
            
            nodeUpdated = true;
        }
        
        if (protectionBits.HasValue && node.Mode != (uint)protectionBits)
        {
            node.Mode = (uint)protectionBits;
            nodeUpdated = true;
        }

        if (!(node.Comment ?? String.Empty).Equals(comment ?? string.Empty))
        {
            node.Comment = comment;
            nodeUpdated = true;
        }

        if (!nodeUpdated)
        {
            return;
        }
        
        var newNodeBytes = UaeFsDbWriter.Build(node);

        if (!hasNode)
        {
            stream.Seek(0, SeekOrigin.End);
        }

        await stream.WriteAsync(newNodeBytes, 0, newNodeBytes.Length);
    }

    public static async Task WriteUaeMetafile(string dirPath, string normalName, int? protectionBits, DateTime? date, string comment)
    {
        var uaeMetafile = new UaeMetafile
        {
            ProtectionBits = EntryFormatter.FormatProtectionBits(
                (ProtectionBits)((protectionBits ?? 0) ^ 0xf)).ToLower(), // mask away "RWED" protection bits
            Date = date ?? DateTime.Now,
            Comment = comment ?? string.Empty
        };

        var uaeMetafileBytes = UaeMetafileWriter.Build(uaeMetafile);

        var uaeMetafilePath = Path.Combine(dirPath, string.Concat(normalName,
            Amiga.DataTypes.UaeMetafiles.Constants.UaeMetafileExtension));

        await File.WriteAllBytesAsync(uaeMetafilePath, uaeMetafileBytes);
    }
}