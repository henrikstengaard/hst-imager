using System;

namespace Hst.Imager.Core.FileInfos;

/// <summary>
/// Represents the Linux stat structure returned by the stat() system call, which provides information about a file or directory.
/// </summary>
/// <param name="DeviceId">Device ID of the file system containing the file.</param>
/// <param name="INodeNumber">INode number of the file.</param>
/// <param name="NumberOfHardlinks">Number of hardlinks to the file.</param>
/// <param name="FileMode">File mode (permissions) of the file.</param>
/// <param name="Uid">User ID of the file owner.</param>
/// <param name="Gid">Group ID of the file owner.</param>
/// <param name="DeviceType">Device type (if the file is a special device file).</param>
/// <param name="Size">Size of the file in bytes.</param>
/// <param name="BlockSize">Block size of the file system.</param>
/// <param name="Blocks">Number of blocks allocated for the file.</param>
/// <param name="LastAccessTime">Last access time of the file.</param>
/// <param name="LastModificationTime">Last modification time of the file.</param>
/// <param name="LastStatusChangeTime">Last status change time of the file.</param>
public record Stat(
    ulong DeviceId,
    ulong INodeNumber,
    ulong NumberOfHardlinks,
    uint FileMode,
    uint Uid,
    uint Gid,
    ulong DeviceType,
    long Size,
    long BlockSize,
    long Blocks,
    DateTime LastAccessTime,
    DateTime LastModificationTime,
    DateTime LastStatusChangeTime);