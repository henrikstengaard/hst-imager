import {formatBytes} from "../utils/Format";
import {get} from "lodash";

export const createRigidDiskBlockParts = ({ media, humanReadable = true }) => {
    const rigidDiskBlock = get(media, 'diskInfo.rigidDiskBlock')
    if (!rigidDiskBlock) {
        return []
    }
    
    let flags = []
    
    if ((rigidDiskBlock.flags & 0x1) === 0x1) {
        flags.push('Last')
    }
    
    if ((rigidDiskBlock.flags & 0x2) === 0x2) {
        flags.push('LastLun')
    }
    
    if ((rigidDiskBlock.flags & 0x4) === 0x4) {
        flags.push('LastId')
    }
    
    if ((rigidDiskBlock.flags & 0x8) === 0x8) {
        flags.push('NoReSelect')
    }
    
    if ((rigidDiskBlock.flags & 0x10) === 0x10) {
        flags.push('DiskId')
    }
    
    if ((rigidDiskBlock.flags & 0x20) === 0x20) {
        flags.push('CtrlRId')
    }
    
    if ((rigidDiskBlock.flags & 0x40) === 0x40) {
        flags.push('Synch')
    }
    
    const rigidDiskBlockPart = {
        type: 'table',
        title: `Rigid Disk Block: ${rigidDiskBlock.diskProduct}, ${formatBytes(rigidDiskBlock.diskSize)}`,
        columns: [{
            name: 'Product',
            align: 'left',
            description: 'Drive Name'
        }, {
            name: 'Vendor',
            align: 'left',
            description: 'Manufacturers Name'
        }, {
            name: 'Revision',
            align: 'left',
            description: 'Drive Revision'
        }, {
            name: 'Size',
            align: 'right',
            description: 'Size of disk'
        }, {
            name: 'Cylinders',
            align: 'right',
            description: 'Number of cylinders'
        }, {
            name: 'Heads',
            align: 'right',
            description: 'Number of heads'
        }, {
            name: 'Sectors',
            align: 'right',
            description: 'Number of blocks per track'
        }, {
            name: 'Block size',
            align: 'right',
            description: 'Size of block'
        }, {
            name: 'Start Cylinder',
            align: 'right',
            description: 'Start cylinder of partitionable disk area'
        }, {
            name: 'End Cylinder',
            align: 'right',
            description: 'End cylinder of partitionable disk area'
        }, {
            name: 'Flags',
            align: 'right',
            description: 'Flags'
        }, {
            name: 'Host Id',
            align: 'right',
            description: 'Host Id'
        }, {
            name: 'RDB Block Lo',
            align: 'right',
            description: 'Start block reserved for RDB'
        }, {
            name: 'RDB Block Hi',
            align: 'right',
            description: 'End block reserved for RDB'
        }],
        rows: [{
            values: [
                rigidDiskBlock.diskVendor,
                rigidDiskBlock.diskProduct,
                rigidDiskBlock.diskRevision,
                humanReadable ? formatBytes(rigidDiskBlock.diskSize) : rigidDiskBlock.diskSize,
                rigidDiskBlock.cylinders,
                rigidDiskBlock.heads,
                rigidDiskBlock.sectors,
                rigidDiskBlock.blockSize,
                rigidDiskBlock.loCylinder,
                rigidDiskBlock.hiCylinder,
                `${rigidDiskBlock.flags} *`,
                rigidDiskBlock.hostId,
                rigidDiskBlock.rdbBlockLo,
                rigidDiskBlock.rdbBlockHi
            ]
        },{
            colspan: 14,
            values: [
                `* Flags ${rigidDiskBlock.flags} = ${flags.join(' ')}`
            ]
        }]
    }

    const fileSystemHeaderBlocks = get(rigidDiskBlock, 'fileSystemHeaderBlocks')
    const fileSystemsPart = fileSystemHeaderBlocks ? {
        type: 'table',
        title: 'Rigid Disk Block: File systems',
        columns: [{
            name: 'Number',
            align: 'right',
        },{
            name: 'DOS Type',
            align: 'left',
        },{
            name: 'Version',
            align: 'left'
        },{
            name: 'File system name',
            align: 'left',
        },{
            name: 'Size',
            align: 'right'
        }],
        rows: fileSystemHeaderBlocks.map((fileSystem, index) => {        
            return {
                values: [
                    index + 1,
                    `${fileSystem.dosTypeHex} (${fileSystem.dosTypeFormatted})`,
                    fileSystem.versionFormatted,
                    fileSystem.fileSystemName,
                    humanReadable ? formatBytes(fileSystem.size) : fileSystem.size
                ]
            }
        })
    } : null

    const partitionBlocks = get(rigidDiskBlock, 'partitionBlocks')
    const partitionPart = partitionBlocks ? {
        type: 'table',
        title: 'Rigid Disk Block: Partitions',
        columns: [{
            name: 'Number',
            align: 'right',
            description: 'Partition number'
        }, {
            name: 'Name',
            align: 'left',
            description: 'Partition name'
        }, {
            name: 'Size',
            align: 'left',
            description: 'Size of partition'
        }, {
            name: 'Start Cylinder',
            align: 'right',
            description: 'Start cylinder of partition'
        }, {
            name: 'End Cylinder',
            align: 'right',
            description: 'End cylinder of partition'
        }, {
            name: 'Total Cylinders',
            align: 'right',
            description: 'Total cylinders'
        }, {
            name: 'Heads',
            align: 'right',
            description: 'Number of heads'
        }, {
            name: 'Blocks per Track',
            align: 'right',
            description: 'Number of blocks per track'
        }, {
            name: 'Buffers',
            align: 'right',
            description: 'Number of buffers'
        }, {
            name: 'File System Block Size',
            align: 'right',
            description: 'File system block size'
        }, {
            name: 'Reserved',
            align: 'right',
            description: 'Reserved'
        }, {
            name: 'PreAlloc',
            align: 'right',
            description: 'PreAlloc'
        }, {
            name: 'Bootable',
            align: 'left',
            description: 'Bootable'
        }, {
            name: 'Boot priority',
            align: 'right',
            description: 'Boot priority'
        }, {
            name: 'No mount',
            align: 'left',
            description: 'No mount'
        }, {
            name: 'DOS Type',
            align: 'left',
            description: 'DOS Type'
        }, {
            name: 'Mask',
            align: 'left',
            description: 'Mask'
        }, {
            name: 'Max Transfer',
            align: 'left',
            description: 'Max Transfer'
        }],
        rows: partitionBlocks.map((partition, index) => {
            return {
                values: [
                    (index + 1),
                    partition.driveName,
                    humanReadable ? formatBytes(partition.partitionSize) : partition.partitionSize,
                    partition.lowCyl,
                    partition.highCyl,
                    (partition.highCyl - partition.lowCyl + 1),
                    partition.surfaces,
                    partition.blocksPerTrack,
                    partition.numBuffer,
                    partition.fileSystemBlockSize,
                    partition.reserved,
                    partition.preAlloc,
                    partition.bootable ? 'Yes' : 'No',
                    partition.bootPriority,
                    partition.noMount ? 'Yes' : 'No',
                    `${partition.dosTypeHex} (${partition.dosTypeFormatted})`,
                    `${partition.maskHex} (${partition.mask})`,
                    `${partition.maxTransferHex} (${partition.maxTransfer})`
                ]
            }
        })
    } : null
    
    let rdbParts = [rigidDiskBlockPart]
    
    if (fileSystemsPart) { 
        rdbParts.push(fileSystemsPart)
    }
    
    if (partitionPart) {
        rdbParts.push(partitionPart)
    }
    
    return rdbParts;
}