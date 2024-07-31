using System;

namespace Hst.Imager.Core.PartitionTables;

[Flags]
public enum RigidDiskBlockFlagsEnum
{
    /// <summary>
    /// no disks exist to be configured after this one on this controller
    /// </summary>
    Last = 0x1,
    /// <summary>
    /// no LUNs exist to be configured greater than this one at this SCSI Target ID
    /// </summary>
    LastLun = 0x2,
    /// <summary>
    /// no Target IDs exist to be configured greater than this one on this SCSI bus
    /// </summary>
    LastId = 0x4,
    /// <summary>
    /// don't bother trying to perform reselection when talking to this drive
    /// </summary>
    NoReSelect = 0x8,
    /// <summary>
    /// rdb_Disk... identification valid
    /// </summary>
    DiskId = 0x10,
    /// <summary>
    /// rdb_Controller...identification valid
    /// </summary>
    CtrlRId = 0x20,
    /// <summary>
    /// drive supports scsi synchronous mode, DANGEROUS TO USE IF IT DOESN'T (added 7/20/89 by commodore)
    /// </summary>
    Synch = 0x40
}