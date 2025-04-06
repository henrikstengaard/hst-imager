import {formatBytes} from "./Format";
import {get} from "lodash";

export const getPartPathOptions = (media) => {
    // build part path options based on media  
    const partPathOptions = [{
        title: `Disk (${formatBytes(media.diskSize)})`,
        value: media.path
    }]

    // return if media is compressed raw or compressed image
    if (media.type === 'CompressedRaw' || media.type === 'CompressedImg') {
        return partPathOptions
    }

    const directoryPathSeparator = media.path.startsWith('/') ? '/' : '\\'

    const gptPartitionTablePart = get(media, 'diskInfo.gptPartitionTablePart');
    if (gptPartitionTablePart) {
        partPathOptions.push({
            title: `Guid Partition Table (${formatBytes(gptPartitionTablePart.size)})`,
            value: media.path + directoryPathSeparator + 'gpt'
        })

        gptPartitionTablePart.parts.filter(part => part.partType === 'Partition').forEach(part => {
            const type = part.partitionType === part.fileSystem
                ? part.partitionType
                : `${part.partitionType}, ${part.fileSystem}`;

            partPathOptions.push({
                title: `Partition #${part.partitionNumber}: ${type} (${formatBytes(part.size)})`,
                value: media.path + directoryPathSeparator + 'gpt' + directoryPathSeparator + part.partitionNumber
            })
        })
    }

    const mbrPartitionTablePart = get(media, 'diskInfo.mbrPartitionTablePart');
    if (mbrPartitionTablePart) {
        partPathOptions.push({
            title: `Master Boot Record (${formatBytes(mbrPartitionTablePart.size)})`,
            value: media.path + directoryPathSeparator + 'mbr'
        })

        mbrPartitionTablePart.parts.filter(part => part.partType === 'Partition').forEach(part => {
            const type = part.partitionType === part.fileSystem
                ? part.partitionType
                : `${part.partitionType}, ${part.fileSystem}`;

            partPathOptions.push({
                title: `Partition #${part.partitionNumber}: ${type} (${formatBytes(part.size)})`,
                value: media.path + directoryPathSeparator + 'mbr' + directoryPathSeparator + part.partitionNumber
            })
        })
    }

    const rdbPartitionTablePart = get(media, 'diskInfo.rdbPartitionTablePart');
    if (rdbPartitionTablePart) {
        partPathOptions.push({
            title: `Rigid Disk Block (${formatBytes(rdbPartitionTablePart.size)})`,
            value: media.path + directoryPathSeparator + 'rdb'
        })

        rdbPartitionTablePart.parts.filter(part => part.partType === 'Partition').forEach(part => {
            const type = part.partitionType === part.fileSystem
                ? part.partitionType
                : `${part.partitionType}, ${part.fileSystem}`;

            partPathOptions.push({
                title: `Partition #${part.partitionNumber}: ${type} (${formatBytes(part.size)})`,
                value: media.path + directoryPathSeparator + 'rdb' + directoryPathSeparator + part.partitionNumber
            })
        })
    }

    partPathOptions.push({
        title: 'Custom',
        value: 'custom'
    })

    return partPathOptions;
}