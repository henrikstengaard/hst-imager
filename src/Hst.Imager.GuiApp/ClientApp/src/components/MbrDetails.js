import {formatBytes} from "../utils/Format";
import {get} from "lodash";

export const createMasterBootRecordParts = ({ media, humanReadable = true }) => {
    const mbrPartitionTablePart = get(media, 'diskInfo.mbrPartitionTablePart')
    if (!mbrPartitionTablePart) {
        return []
    }

    const diskGeometry = get(mbrPartitionTablePart, 'diskGeometry')
    if (!diskGeometry) {
        return []
    }
    
    const geometryPart = {
        type: 'table',
        title: 'Master Boot Record: Geometry',
        columns: [{
            name: 'Size',
            align: humanReadable ? 'left' : 'right'
        },{
            name: 'Sector size',
            align: 'right'
        },{
            name: 'Total sectors',
            align: 'right'
        },{
            name: 'Cylinders',
            align: 'right'
        },{
            name: 'Heads per cylinder',
            align: 'right'
        },{
            name: 'Sectors per track',
            align: 'right'
        }],
        rows: [{
            values: [
                humanReadable ? formatBytes(diskGeometry.capacity) : diskGeometry.capacity,
                diskGeometry.bytesPerSector,
                diskGeometry.totalSectors,
                diskGeometry.cylinders,
                diskGeometry.headsPerCylinder,
                diskGeometry.sectorsPerTrack
            ]
        }]
    }
    
    const formatBiosType = (id) => {
        const idIntValue = parseInt(id);
        return `0x${idIntValue.toString(16)}`;
    }

    const parts = get(mbrPartitionTablePart, 'parts')
    const partitionsPart = parts ? {
        type: 'table',
        title: `Master Boot Record: Partitions`,
        columns: [{
            name: 'Number',
            align: 'right'
        },{
            name: 'Id',
            align: 'right'
        },{
            name: 'Type',
            align: 'left'
        },{
            name: 'File System',
            align: 'left'
        },{
            name: 'Size',
            align: humanReadable ? 'left' : 'right'
        },{
            name: 'Start Sector',
            align: 'right'
        },{
            name: 'End Sector',
            align: 'right'
        },{
            name: 'Active',
            align: 'left'
        },{
            name: 'Primary',
            align: 'left'
        }],
        rows: parts.filter((partition) => partition.partType === 'Partition').map((partition) => {
            return {
                values: [
                    partition.partitionNumber || '',
                    formatBiosType(partition.biosType),
                    partition.partitionType,
                    partition.fileSystem,
                    humanReadable ? formatBytes(partition.size) : partition.size,
                    partition.startSector,
                    partition.endSector,
                    partition.isActive ? 'Yes' : 'No',
                    partition.isPrimary ? 'Yes' : 'No'
                ]
            }
        })
    } : null
    
    let mbrParts = [geometryPart]

    if (partitionsPart) {
        mbrParts.push(partitionsPart)
    }
    
    return mbrParts;
}