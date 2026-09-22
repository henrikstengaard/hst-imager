using System;
using System.Buffers.Binary;
using System.Text;

namespace Hst.Imager.Core.FileInfos;

/// <summary>
/// Helpers for reading values from a raw native "struct stat" buffer.
/// </summary>
internal static class NativeStatHelper
{
    /// <summary>
    /// Size of the buffer allocated for a native "struct stat".
    /// Deliberately much larger than any known platform layout, so a layout
    /// mismatch can never cause the kernel to write outside the buffer.
    /// </summary>
    public const int StatBufferSize = 512;

    public static byte[] ToNullTerminatedUtf8(string path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        var bytes = new byte[Encoding.UTF8.GetByteCount(path) + 1];
        Encoding.UTF8.GetBytes(path, 0, path.Length, bytes, 0);
        return bytes;
    }

    public static ushort ReadUInt16(byte[] buffer, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(offset, sizeof(ushort)));

    public static uint ReadUInt32(byte[] buffer, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset, sizeof(uint)));

    public static int ReadInt32(byte[] buffer, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset, sizeof(int)));

    public static ulong ReadUInt64(byte[] buffer, int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(offset, sizeof(ulong)));

    public static long ReadInt64(byte[] buffer, int offset) =>
        BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(offset, sizeof(long)));

    /// <summary>
    /// Converts unix time seconds to utc date time, clamping values outside of
    /// the supported range instead of throwing.
    /// </summary>
    public static DateTime ToUtcDateTime(long unixTimeSeconds)
    {
        const long minUnixTimeSeconds = -62135596800L;
        const long maxUnixTimeSeconds = 253402300799L;

        var seconds = unixTimeSeconds < minUnixTimeSeconds
            ? minUnixTimeSeconds
            : unixTimeSeconds > maxUnixTimeSeconds
                ? maxUnixTimeSeconds
                : unixTimeSeconds;

        return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
    }
}
