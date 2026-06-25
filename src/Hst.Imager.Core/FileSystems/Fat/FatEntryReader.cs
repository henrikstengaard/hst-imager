using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core.Converters;

namespace Hst.Imager.Core.FileSystems.Fat;

public static class FatEntryReader
{
    public static FatEntry ReadEntry(byte[] data, int offset)
    {
        var name = Encoding.ASCII.GetString(data, offset, 11);
        var attribute = data[offset + 0xb];
        var reserved = data[offset + 0xc];

        // var creationHour = data[offset + 0xf] >> 3;
        // var creationMinute = (data[offset + 0xe] >> 5) + ((data[offset + 0xf] & 0x7) << 3);
        // var creationSecond = (data[offset + 0xe] & 0x1f) * 2;
        var creationTenth = data[offset + 0xd];
        var (creationHour, creationMinute, creationSecond) =
            DateTimeConverter.ConvertUInt16BytesToTime(data, offset + 0xe);

        // increase creation seconds by 1, if creation time tenth is above 100
        if (creationTenth > 100)
        {
            creationSecond++;
        }

        // var creationYear = 1980 + (data[offset + 0x11] >> 1); // uint16 little endian bits 15-9
        // var creationMonth = ((data[offset + 0x11] & 0x1) << 3) + (data[offset + 0x10] >> 5); // uint16 little endian bits 8-5
        // var creationDay = data[offset + 0x10] & 0x1f; // uint16 little endian bits 4-0
        var (creationYear, creationMonth, creationDay) =
            DateTimeConverter.ConvertUInt16BytesToDate(data, offset + 0x10);

        // var lastAccessYear = 1980 + (data[offset + 0x13] >> 1); // uint16 little endian bits 15-9
        // var lastAccessMonth = ((data[offset + 0x13] & 0x1) << 3) + (data[offset + 0x12] >> 5); // uint16 little endian bits 8-5
        // var lastAccessDay = data[offset + 0x12] & 0x1f; // uint16 little endian bits 4-0
        var lastAccessDate = ReadLastAccessDate(data, offset + 0x12);

        var highFirstCluster = LittleEndianConverter.ConvertBytesToUInt16(data, offset + 0x14);
        
        // var modificationHour = data[offset + 0x17] >> 3;
        // var modificationMinute = (data[offset + 0x16] >> 5) + ((data[offset + 0x17] & 0x7) << 3);
        // var modificationSecond = (data[offset + 0x16] & 0x1f) * 2;
        var (modificationHour, modificationMinute, modificationSecond) =
            DateTimeConverter.ConvertUInt16BytesToTime(data, offset + 0x16);

        // var modificationYear = 1980 + (data[offset + 0x19] >> 1); // uint16 little endian bits 15-9
        // var modificationMonth = ((data[offset + 0x19] & 0x1) << 3) + (data[offset + 0x18] >> 5); // uint16 little endian bits 8-5
        // var modificationDay = data[offset + 0x18] & 0x1f; // uint16 little endian bits 4-0
        var (modificationYear, modificationMonth, modificationDay) =
            DateTimeConverter.ConvertUInt16BytesToDate(data, offset + 0x18);

        var lowFirstCluster = LittleEndianConverter.ConvertBytesToUInt16(data, offset + 0x1a);
        var size = LittleEndianConverter.ConvertBytesToUInt32(data, offset + 0x1c);
        
        return new FatEntry
        {
            Name = name,
            Attribute = attribute,
            CreationDate = new DateTime(creationYear, creationMonth, creationDay, creationHour, creationMinute, creationSecond, DateTimeKind.Local),
            LastAccessDate = lastAccessDate,
            ModificationDate = new DateTime(modificationYear, modificationMonth, modificationDay, modificationHour, modificationMinute, modificationSecond, DateTimeKind.Local),
            LowFirstCluster = lowFirstCluster,
            HighFirstCluster = highFirstCluster,
            Size = size
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
    public static async Task<IEnumerable<FatEntry>> ReadEntries(Stream stream, long clusterOffset, int clusterSize,
        CancellationToken cancellationToken = default)
    {
        stream.Seek(clusterOffset, SeekOrigin.Begin);
        
        var clusterBytes = new byte[clusterSize];
        await stream.ReadExactlyAsync(clusterBytes, 0, clusterBytes.Length, cancellationToken);

        var fat32Entries = new List<FatEntry>();
        for (var entryOffset = 0; entryOffset < clusterBytes.Length; entryOffset += 32)
        {
            if (IsFatEntryUnallocated(clusterBytes, entryOffset) ||
                IsFatEntryDeleted(clusterBytes, entryOffset))
            {
                continue;
            }

            var entry = ReadEntry(clusterBytes, entryOffset);

            fat32Entries.Add(entry);
        }

        return fat32Entries;
    }

    private static bool IsFatEntryUnallocated(byte[] data, int offset) => data[offset] == 0;
    private static bool IsFatEntryDeleted(byte[] data, int offset) => data[offset] == 0xe5;
}