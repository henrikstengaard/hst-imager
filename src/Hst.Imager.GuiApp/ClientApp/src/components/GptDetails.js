import {formatBytes} from "../utils/Format";
import {get} from "lodash";

export const createGuidPartitionTableParts = ({ media, humanReadable = true }) => {
    const gptPartitionTablePart = get(media, 'diskInfo.gptPartitionTablePart')
    if (!gptPartitionTablePart) {
        return []
    }

    const diskGeometry = get(gptPartitionTablePart, 'diskGeometry')
    if (!diskGeometry) {
        return []
    }

    const geometryPart = {
        type: 'table',
        title: 'Guid Partition Table: Geometry',
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

    const parts = get(gptPartitionTablePart, 'parts')
    const partitionsPart = parts ? {
        type: 'table',
        title: `Guid Partition Table: Partitions`,
        columns: [{
            name: 'Number',
            align: 'right'
        },{
            name: 'Guid',
            align: 'left'
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
        }],
        rows: parts.filter((partition) => partition.partType === 'Partition').map((partition) => {
            return {
                values: [
                    partition.partitionNumber || '',
                    partition.guidType,
                    partition.partitionType,
                    partition.fileSystem,
                    humanReadable ? formatBytes(partition.size) : partition.size,
                    partition.startSector,
                    partition.endSector
                ]
            }
        })
    } : null

    let gptParts = [geometryPart]

    if (partitionsPart) {
        gptParts.push(partitionsPart)
    }

    return gptParts;
}