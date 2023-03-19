import {formatBytes} from "../utils/Format";
import {get, isNil, set} from "lodash";
import TableRow from "@mui/material/TableRow";
import TableCell from "@mui/material/TableCell";
import React from "react";
import TableContainer from "@mui/material/TableContainer";
import Paper from "@mui/material/Paper";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import Typography from "@mui/material/Typography";
import AccordionSummary from "@mui/material/AccordionSummary";
import {styled} from "@mui/material/styles";

const StyledAccordionSummary = styled(AccordionSummary)(({theme}) => ({
    padding: 0,
    color: theme.palette.primary.main
}));

export default function MediaDetails(props) {
    const {
        media
    } = props

    const [state, setState] = React.useState({});

    const handleChange = (id, expanded) => {
        set(state, id, !expanded)
        setState({...state})
    }
    
    const disk = {
        label: `Disk: ${media.name}, ${formatBytes(media.diskSize)}`,
        fields: [{
            label: 'Name',
            value: media.name
        }, {
            label: 'Path',
            value: media.path
        }, {
            label: 'Size',
            value: `${formatBytes(media.diskSize)} (${media.diskSize} bytes)`
        }]
    }

    const rigidDiskBlock = media.diskInfo.rigidDiskBlock ? {
        label: `Rigid Disk Block: ${media.diskInfo.rigidDiskBlock.diskProduct}, ${formatBytes(media.diskInfo.rigidDiskBlock.diskSize)}`,
        fields: [{
            label: 'Manufacturers Name',
            value: media.diskInfo.rigidDiskBlock.diskVendor
        }, {
            label: 'Drive Name',
            value: media.diskInfo.rigidDiskBlock.diskProduct
        }, {
            label: 'Drive Revision',
            value: media.diskInfo.rigidDiskBlock.diskRevision
        }, {
            label: 'Size',
            value: `${formatBytes(media.diskInfo.rigidDiskBlock.diskSize)} (${media.diskInfo.rigidDiskBlock.diskSize} bytes)`
        }, {
            label: 'Cylinders',
            value: media.diskInfo.rigidDiskBlock.cylinders
        }, {
            label: 'Heads',
            value: media.diskInfo.rigidDiskBlock.heads
        }, {
            label: 'Blocks per Track',
            value: media.diskInfo.rigidDiskBlock.sectors
        }, {
            label: 'Blocks per Cylinder',
            value: media.diskInfo.rigidDiskBlock.cylBlocks
        }, {
            label: 'Block size',
            value: media.diskInfo.rigidDiskBlock.blockSize
        }, {
            label: 'Park head cylinder',
            value: media.diskInfo.rigidDiskBlock.parkingZone
        }, {
            label: 'Start cylinder of partitionable disk area',
            value: media.diskInfo.rigidDiskBlock.loCylinder
        }, {
            label: 'End cylinder of partitionable disk area',
            value: media.diskInfo.rigidDiskBlock.hiCylinder
        }, {
            label: 'Start block reserved for RDB',
            value: media.diskInfo.rigidDiskBlock.rdbBlockLo
        }, {
            label: 'End block reserved for RDB',
            value: media.diskInfo.rigidDiskBlock.rdbBlockHi
        }]
    } : null
    
    const fileSystems = (get(media, 'diskInfo.rigidDiskBlock.fileSystemHeaderBlocks') || []).map((fileSystem, index) => {
        return {
            label: `Rigid Disk Block, File system ${(index + 1)}: ${fileSystem.dosTypeFormatted}`,
            fields: [{
                label: 'DOS Type',
                value: `${fileSystem.dosTypeHex} (${fileSystem.dosTypeFormatted})`
            }, {
                label: 'Version',
                value: fileSystem.versionFormatted
            }, {
                label: 'File system name',
                value: fileSystem.fileSystemName
            }, {
                label: 'Size',
                value: `${formatBytes(fileSystem.size)} (${fileSystem.size} bytes)`
            }]
        }
    })

    const partitions = (get(media, 'diskInfo.rigidDiskBlock.partitionBlocks') || []).map((partition, index) => {
        return {
            label: `Rigid Disk Block, Partition ${(index + 1)}: ${partition.driveName}, ${formatBytes(partition.partitionSize)}`,
            fields: [{
                label: 'Device Name',
                value: partition.driveName
            }, {
                label: 'Size',
                value: `${formatBytes(partition.partitionSize)} (${partition.partitionSize} bytes)`
            }, {
                label: 'Start Cylinder',
                value: partition.lowCyl
            }, {
                label: 'End Cylinder',
                value: partition.highCyl
            }, {
                label: 'Total Cylinders',
                value: (partition.highCyl - partition.lowCyl + 1)
            }, {
                label: 'Heads',
                value: partition.surfaces
            }, {
                label: 'Blocks per Track',
                value: partition.blocksPerTrack
            }, {
                label: 'Buffers',
                value: partition.numBuffer
            }, {
                label: 'File System Block Size',
                value: partition.fileSystemBlockSize
            }, {
                label: 'Reserved',
                value: partition.reserved
            }, {
                label: 'PreAlloc',
                value: partition.preAlloc
            }, {
                label: 'Bootable',
                value: partition.bootable ? 'Yes' : 'No'
            }, {
                label: 'Boot priority',
                value: partition.bootPriority
            }, {
                label: 'No mount',
                value: partition.noMount ? 'Yes' : 'No'
            }, {
                label: 'DOS Type',
                value: `${partition.dosTypeHex} (${partition.dosTypeFormatted})`
            }, {
                label: 'Mask',
                value: `${partition.maskHex} (${partition.mask})`
            }, {
                label: 'Max Transfer',
                value: `${partition.maxTransferHex} (${partition.maxTransfer})`
            }]
        }
    })

    let sections = [disk]
    if (rigidDiskBlock) {
        sections = sections.concat([rigidDiskBlock])
    }

    sections = sections.concat(fileSystems).concat(partitions)

    const renderFields = (fields) => {
        if (!fields) {
            return null
        }
        return fields.map((field, index) => {
            return (
                <TableRow key={index}>
                    <TableCell>
                        {field.label}
                    </TableCell>
                    <TableCell>
                        {field.value}
                    </TableCell>
                </TableRow>
            )
        })
    }
    
    return (
        <TableContainer component={Paper}>
            <Table size="small" aria-label="media details">
                <TableBody>
                    {sections.map((section, index) => {
                        const id = index.toString()
                        const expanded = isNil(state[id]) ? true : get(state , id)
                        return (
                            <React.Fragment key={index}>
                                <TableRow>
                                    <TableCell colSpan="2">
                                        <StyledAccordionSummary
                                            expandIcon={<FontAwesomeIcon icon={expanded ? 'chevron-up' : 'chevron-down'}/>}
                                            onClick={() => handleChange(index, expanded)}
                                        >
                                            <Typography>
                                                {section.label}
                                            </Typography>
                                        </StyledAccordionSummary>
                                    </TableCell>
                                </TableRow>
                                {expanded && renderFields(section.fields)}
                            </React.Fragment>
                        )}
                    )}
                </TableBody>
            </Table>
        </TableContainer>
    )
}