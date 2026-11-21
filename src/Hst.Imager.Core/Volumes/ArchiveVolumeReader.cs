using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems;
using Hst.Compression.Lha;
using Hst.Compression.Lzx;
using Hst.Imager.Core.Compressions.Zip;
using Hst.Imager.Core.FileSystems;
using Hst.Imager.Core.Models;
using Hst.Imager.Core.PathComponents;
using ICSharpCode.SharpZipLib.Lzw;

namespace Hst.Imager.Core.Volumes;

/// <summary>
/// Reads archives into read-only archive volumes.
/// </summary>
public static partial class ArchiveVolumeReader
{
    private sealed class Disposables(params IDisposable[] disposables) : IDisposable
    {
        public void Dispose()
        {
            foreach (var disposable in disposables)
            {
                disposable?.Dispose();
            }
        }
    }

    /// <summary>
    /// Read zip archive from stream.
    /// </summary>
    public static async Task<ArchiveVolume> ReadZip(Stream stream)
    {
        var mediaPath = MediaPath.ZipArchivePath;

        var attributesMode = AttributesMode.Auto;
        var centralDirectoryFileHeaderIndex = new Dictionary<string, CentralDirectoryFileHeader>();

        stream.Seek(0, SeekOrigin.Begin);
        var zipArchiveReader = new ZipArchiveReader(stream);
        while (await zipArchiveReader.Read() is { } zipHeader)
        {
            if (zipHeader is not CentralDirectoryFileHeader centralDirectoryFileHeader)
            {
                continue;
            }

            attributesMode = (HostOsFlags)centralDirectoryFileHeader.HostOs switch
            {
                HostOsFlags.MsDos => AttributesMode.Windows,
                HostOsFlags.Amiga => AttributesMode.Amiga,
                _ => AttributesMode.Auto
            };

            centralDirectoryFileHeaderIndex[centralDirectoryFileHeader.FileName] = centralDirectoryFileHeader;
        }

        stream.Seek(0, SeekOrigin.Begin);
        var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);

        var items = zipArchive.Entries.OrderBy(x => x.FullName).Select(zipEntry =>
        {
            var isDir = zipEntry.FullName.EndsWith('/');
            var entryPath = isDir ? zipEntry.FullName[..^1] : zipEntry.FullName;
            var properties = GetZipProperties(centralDirectoryFileHeaderIndex.GetValueOrDefault(zipEntry.FullName));

            return new ArchiveItem(mediaPath.Split(entryPath), isDir, zipEntry.LastWriteTime.LocalDateTime,
                zipEntry.Length, properties, entryPath, () => Task.FromResult(zipEntry.Open()));
        }).ToList();

        return new ArchiveVolume(mediaPath, attributesMode, true, items, new Disposables(zipArchive, stream));
    }

    private static Dictionary<string, string> GetZipProperties(CentralDirectoryFileHeader centralDirectoryFileHeader)
    {
        var properties = new Dictionary<string, string>();

        if (centralDirectoryFileHeader == null)
        {
            return properties;
        }

        if (!string.IsNullOrEmpty(centralDirectoryFileHeader.FileComment))
        {
            properties[Constants.EntryPropertyNames.Comment] = centralDirectoryFileHeader.FileComment;
        }

        switch ((HostOsFlags)centralDirectoryFileHeader.HostOs)
        {
            case HostOsFlags.MsDos:
                var fileAttributes = (int)centralDirectoryFileHeader.ExternalFileAttributes;
                properties[Constants.EntryPropertyNames.WindowsAttributes] = fileAttributes.ToString();
                break;
            case HostOsFlags.Amiga:
                var protectionBitsValue = (int)((centralDirectoryFileHeader.ExternalFileAttributes >> 16) & 0xff);
                properties[Constants.EntryPropertyNames.ProtectionBits] = (protectionBitsValue ^ 0xf).ToString();
                break;
        }

        return properties;
    }

    /// <summary>
    /// Read lha archive from stream.
    /// </summary>
    public static async Task<ArchiveVolume> ReadLha(Stream stream)
    {
        var mediaPath = MediaPath.LhaArchivePath;
        var lhaArchive = new LhaArchive(stream, LhaOptions.AmigaLhaOptions);

        var items = new List<ArchiveItem>();
        foreach (var lhaEntry in await lhaArchive.Entries())
        {
            // skip lha entry, if name is empty.
            // shareware version of picasso96.lha from aminet has 2 files in root directory without filename.
            if (string.IsNullOrWhiteSpace(lhaEntry.Name))
            {
                continue;
            }

            var entryPath = GetLhaEntryName(lhaEntry.Name);
            var isDir = entryPath.EndsWith('\\');

            var protectionBits = ProtectionBitsConverter.ToProtectionBits(lhaEntry.Attribute);
            var properties = new Dictionary<string, string>
            {
                { Constants.EntryPropertyNames.ProtectionBits, ((int)protectionBits ^ 0xf).ToString() }
            };

            var comment = GetLhaEntryComment(lhaEntry.Name);
            if (!string.IsNullOrEmpty(comment))
            {
                properties.Add(Constants.EntryPropertyNames.Comment, comment);
            }

            var header = lhaEntry;
            items.Add(new ArchiveItem(mediaPath.Split(entryPath), isDir, lhaEntry.UnixLastModifiedStamp,
                lhaEntry.OriginalSize, properties, entryPath, () =>
                {
                    var output = new MemoryStream();
                    lhaArchive.Extract(header, output);
                    output.Position = 0;
                    return Task.FromResult<Stream>(output);
                }));
        }

        return new ArchiveVolume(mediaPath, AttributesMode.Amiga, false, items,
            new Disposables(lhaArchive, stream));
    }

    // get lha entry name by stripping away other chars after zero byte
    private static string GetLhaEntryName(string name)
    {
        var index = name.IndexOf('\0');
        return index >= 0 ? name[..index] : name;
    }

    // get lha entry comment after zero byte
    private static string GetLhaEntryComment(string name)
    {
        var index = name.IndexOf('\0');
        return index >= 0 && index < name.Length - 1 ? name[(index + 1)..] : string.Empty;
    }

    /// <summary>
    /// Read lzx archive from stream.
    /// </summary>
    public static async Task<ArchiveVolume> ReadLzx(Stream stream)
    {
        var mediaPath = MediaPath.LzxArchivePath;
        var lzxArchive = new LzxArchive(stream, LzxOptions.AmigaLzxOptions);

        var items = new List<ArchiveItem>();
        while (await lzxArchive.Next() is { } lzxEntry)
        {
            // extract lzx entry, lzx archive can only be read sequentially
            byte[] data;
            using (var memoryStream = new MemoryStream())
            {
                await lzxArchive.Extract(memoryStream);
                data = memoryStream.ToArray();
            }

            var entryPath = lzxEntry.Name;
            var isDir = entryPath.EndsWith(mediaPath.PathSeparator);

            var protectionBits = GetLzxProtectionBits(lzxEntry.Attributes);
            var properties = new Dictionary<string, string>
            {
                { Constants.EntryPropertyNames.ProtectionBits, ((int)protectionBits ^ 0xf).ToString() }
            };

            if (!string.IsNullOrEmpty(lzxEntry.Comment))
            {
                properties.Add(Constants.EntryPropertyNames.Comment, lzxEntry.Comment);
            }

            items.Add(new ArchiveItem(mediaPath.Split(entryPath), isDir, lzxEntry.Date, lzxEntry.UnpackedSize,
                properties, entryPath, () => Task.FromResult<Stream>(new MemoryStream(data))));
        }

        return new ArchiveVolume(mediaPath, AttributesMode.Amiga, true, items, stream);
    }

    private static ProtectionBits GetLzxProtectionBits(AttributesEnum attributes)
    {
        var protectionBits = ProtectionBits.None;

        if (attributes.HasFlag(AttributesEnum.HeldResident)) protectionBits |= ProtectionBits.HeldResident;
        if (attributes.HasFlag(AttributesEnum.Script)) protectionBits |= ProtectionBits.Script;
        if (attributes.HasFlag(AttributesEnum.Pure)) protectionBits |= ProtectionBits.Pure;
        if (attributes.HasFlag(AttributesEnum.Archive)) protectionBits |= ProtectionBits.Archive;
        if (attributes.HasFlag(AttributesEnum.Read)) protectionBits |= ProtectionBits.Read;
        if (attributes.HasFlag(AttributesEnum.Write)) protectionBits |= ProtectionBits.Write;
        if (attributes.HasFlag(AttributesEnum.Executable)) protectionBits |= ProtectionBits.Executable;
        if (attributes.HasFlag(AttributesEnum.Delete)) protectionBits |= ProtectionBits.Delete;

        return protectionBits;
    }

    [GeneratedRegex("\\.Z$", RegexOptions.IgnoreCase)]
    private static partial Regex LzwExtensionRegex();

    /// <summary>
    /// Read lzw compressed file as an archive volume with a single file.
    /// </summary>
    public static ArchiveVolume ReadLzw(string path)
    {
        var fileInfo = new FileInfo(path);
        var entryName = LzwExtensionRegex().Replace(fileInfo.Name, string.Empty);

        var items = new[]
        {
            new ArchiveItem([entryName], false, fileInfo.LastWriteTime, fileInfo.Length,
                new Dictionary<string, string>(), fileInfo.FullName,
                () => Task.FromResult<Stream>(new LzwInputStream(File.OpenRead(fileInfo.FullName))))
        };

        return new ArchiveVolume(MediaPath.GenericMediaPath, AttributesMode.Auto, false, items);
    }
}
