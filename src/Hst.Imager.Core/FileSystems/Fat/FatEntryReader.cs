using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Converters;
using Hst.Imager.Core.FileSystems.Fat.Clusters;

namespace Hst.Imager.Core.FileSystems.Fat;

public static class FatEntryReader
{
    public static IFatEntry ReadEntry(byte[] data, int offset)
    {
        var name = new byte[11];
        Array.Copy(data, offset, name, 0, 11);
        
        var attribute = data[offset + 0xb];
        var reserved = data[offset + 0xc];

        // var createdHour = data[offset + 0xf] >> 3;
        // var createdMinute = (data[offset + 0xe] >> 5) + ((data[offset + 0xf] & 0x7) << 3);
        // var createdSecond = (data[offset + 0xe] & 0x1f) * 2;
        var createdTenth = data[offset + 0xd];
        var (createdHour, createdMinute, createdSecond) =
            DateTimeConverter.ConvertUInt16BytesToTime(data, offset + 0xe);

        // increase created seconds by 1, if created time tenth is above 100
        if (createdTenth > 100)
        {
            createdSecond++;
        }

        // var createdYear = 1980 + (data[offset + 0x11] >> 1); // uint16 little endian bits 15-9
        // var createdMonth = ((data[offset + 0x11] & 0x1) << 3) + (data[offset + 0x10] >> 5); // uint16 little endian bits 8-5
        // var createdDay = data[offset + 0x10] & 0x1f; // uint16 little endian bits 4-0
        var (createdYear, createdMonth, createdDay) =
            DateTimeConverter.ConvertUInt16BytesToDate(data, offset + 0x10);

        // var lastAccessYear = 1980 + (data[offset + 0x13] >> 1); // uint16 little endian bits 15-9
        // var lastAccessMonth = ((data[offset + 0x13] & 0x1) << 3) + (data[offset + 0x12] >> 5); // uint16 little endian bits 8-5
        // var lastAccessDay = data[offset + 0x12] & 0x1f; // uint16 little endian bits 4-0
        var lastAccessDate = ReadLastAccessDate(data, offset + 0x12);

        var highFirstCluster = LittleEndianConverter.ConvertBytesToUInt16(data, offset + 0x14);
        
        // var modifiedHour = data[offset + 0x17] >> 3;
        // var modifiedMinute = (data[offset + 0x16] >> 5) + ((data[offset + 0x17] & 0x7) << 3);
        // var modifiedSecond = (data[offset + 0x16] & 0x1f) * 2;
        var (modifiedHour, modifiedMinute, modifiedSecond) =
            DateTimeConverter.ConvertUInt16BytesToTime(data, offset + 0x16);

        // var modifiedYear = 1980 + (data[offset + 0x19] >> 1); // uint16 little endian bits 15-9
        // var modifiedMonth = ((data[offset + 0x19] & 0x1) << 3) + (data[offset + 0x18] >> 5); // uint16 little endian bits 8-5
        // var modifiedDay = data[offset + 0x18] & 0x1f; // uint16 little endian bits 4-0
        var (modifiedYear, modifiedMonth, modifiedDay) =
            DateTimeConverter.ConvertUInt16BytesToDate(data, offset + 0x18);

        var lowFirstCluster = LittleEndianConverter.ConvertBytesToUInt16(data, offset + 0x1a);
        var size = LittleEndianConverter.ConvertBytesToUInt32(data, offset + 0x1c);
        
        return new FatEntry
        {
            Name = name,
            Attribute = attribute,
            Reserved = reserved,
            CreatedDate = new DateTime(createdYear, createdMonth, createdDay, createdHour, createdMinute,
                createdSecond, DateTimeKind.Local),
            LastAccessDate = lastAccessDate,
            ModifiedDate = new DateTime(modifiedYear, modifiedMonth, modifiedDay, modifiedHour, modifiedMinute,
                modifiedSecond, DateTimeKind.Local),
            LowFirstCluster = lowFirstCluster,
            HighFirstCluster = highFirstCluster,
            Size = size
        };
    }

    public static FatLongEntry ReadLongEntry(byte[] data, int offset)
    {
        var order = data[offset];
        
        var name1 = new byte[10];
        Array.Copy(data, offset + 0x1, name1, 0, 10);
        
        var attribute = data[offset + 0xb];
        var type = data[offset + 0xc];
        var checksum = data[offset + 0xd];
        
        var name2 = new byte[12];
        Array.Copy(data, offset + 0xe, name2, 0, 12);
        
        var lowFirstCluster = LittleEndianConverter.ConvertBytesToUInt16(data, offset + 0x1a);

        var name3 = new byte[4];
        Array.Copy(data, offset + 0x1c, name3, 0, 4);
        
        return new FatLongEntry
        {
            Order = order,
            Name1 = name1,
            Attribute = attribute,
            Type = type,
            Checksum = checksum,
            Name2 = name2,
            LowFirstCluster = lowFirstCluster,
            Name3 = name3
        };
    }

    private static DateTime? ReadLastAccessDate(byte[] data, int offset)
    {
        if (data[offset] == 0 && data[offset + 1] == 0)
        {
            return null;
        }

        var (lastAccessYear, lastAccessMonth, lastAccessDay) = 
            DateTimeConverter.ConvertUInt16BytesToDate(data, offset);

        return new DateTime(lastAccessYear, lastAccessMonth, lastAccessDay, 0, 0, 0, DateTimeKind.Local);
    }
    
    /// <summary>
    /// Read fat entries from cluster offset.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="clusterOffset"></param>
    /// <param name="clusterSize"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<IEnumerable<IFatEntry>> ReadEntries(Stream stream, long clusterOffset, int clusterSize,
        CancellationToken cancellationToken = default)
    {
        stream.Seek(clusterOffset, SeekOrigin.Begin);
        
        var clusterBytes = new byte[clusterSize];
        await stream.ReadExactlyAsync(clusterBytes, 0, clusterBytes.Length, cancellationToken);

        var fatEntries = new List<IFatEntry>();
        for (var entryOffset = 0; entryOffset < clusterBytes.Length; entryOffset += 32)
        {
            if (IsFatEntryUnallocated(clusterBytes, entryOffset) ||
                IsFatEntryDeleted(clusterBytes, entryOffset))
            {
                continue;
            }

            var entry = IsFatEntryLongName(clusterBytes, entryOffset)
                ? ReadLongEntry(clusterBytes, entryOffset)
                : ReadEntry(clusterBytes, entryOffset);

            fatEntries.Add(entry);
        }

        return fatEntries;
    }

    private static bool IsFatEntryUnallocated(byte[] data, int offset) => data[offset] == 0;
    private static bool IsFatEntryDeleted(byte[] data, int offset) => data[offset] == 0xe5;

    private const FatAttributeFlags LongNameAttributeFlags = FatAttributeFlags.ReadOnly | FatAttributeFlags.Hidden |
                                                             FatAttributeFlags.System | FatAttributeFlags.VolumeId;
    private const FatAttributeFlags LongNameMaskAttributeFlags = FatAttributeFlags.ReadOnly | FatAttributeFlags.Hidden |
                                                                 FatAttributeFlags.System | FatAttributeFlags.VolumeId |
                                                                 FatAttributeFlags.Directory | FatAttributeFlags.Archive;

    private static bool IsFatEntryLongName(byte[] data, int offset) => (FatAttributeFlags)data[offset + 0xb] == LongNameAttributeFlags;
    private static bool IsFatEntryLongNameMask(byte[] data, int offset) => (FatAttributeFlags)data[offset + 0xb] == LongNameMaskAttributeFlags;
    
    public static async IAsyncEnumerable<IEnumerable<IFatEntry>> ReadRootEntries(Stream stream, long partitionOffset,
        FatFileSystem fatFileSystem, byte[] fatBytes, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var bytesPerSector = fatFileSystem.BiosParameterBlock.BytesPerSector;
        
        if (fatFileSystem.RootDirSectors != 0)
        {
            var rootDirectoryOffset = fatFileSystem.FirstRootSector * bytesPerSector;
            yield return await ReadEntries(stream, partitionOffset + rootDirectoryOffset,
                fatFileSystem.RootDirSectors * bytesPerSector, cancellationToken);

            yield break;
        }

        var clusters = FatCluster.ReadClusterChain(fatFileSystem.FatType, fatBytes,
            fatFileSystem.ExtendedBiosParameterBlock.RootCluster).ToList();
        var clusterSize = fatFileSystem.BiosParameterBlock.SectorsPerCluster * bytesPerSector;

        foreach (var cluster in clusters)
        {
            var clusterSector = fatFileSystem.FirstDataSector + ((cluster - Constants.ReservedClusters) * fatFileSystem.BiosParameterBlock.SectorsPerCluster);
            var clusterOffset = clusterSector * bytesPerSector;
            var fatEntries = (await ReadEntries(stream, partitionOffset + clusterOffset, clusterSize, cancellationToken))
                .ToList();

            yield return fatEntries;
        }
    }

    private static string GetLongEntryName(IList<FatLongEntry> fatLongEntries)
    {
        var name = new StringBuilder(20);
        
        for (var i = fatLongEntries.Count - 1; i >= 0; i--)
        {
            var fatLongEntry = fatLongEntries[i];
            
            
            var nameBytes = new List<byte>();
            nameBytes.AddRange(fatLongEntry.Name1);
            nameBytes.AddRange(fatLongEntry.Name2);
            nameBytes.AddRange(fatLongEntry.Name3);
        }
        
        return name.ToString();
    }

    private static string DecodeUnicodeString(byte[] data, int offset, int count)
    {
        if (offset + count > data.Length)
        {
            throw  new ArgumentException("The specified range exceeds the bounds of the data array.");
        }
        if (count % 2 != 0)
        {
            throw new ArgumentOutOfRangeException($"Count must be even, but was {count}");
        }
        
        var stringLength = 0;
        for (var i = 0; i < count; i+= 2)
        {
            if (data[offset + i] != 0x00 && data[offset + i + 1] != 0x00)
            {
                continue;
            }
            stringLength = i;
            break;
        }
        
        return Encoding.Unicode.GetString(data, offset, stringLength);
    }
}