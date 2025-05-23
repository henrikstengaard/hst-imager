import React, {Fragment} from "react";
import TableContainer from "@mui/material/TableContainer";
import Paper from "@mui/material/Paper";
import Table from "@mui/material/Table";
import {TableHead, Tooltip} from "@mui/material";
import TableRow from "@mui/material/TableRow";
import TableCell from "@mui/material/TableCell";
import TableBody from "@mui/material/TableBody";
import Stack from "@mui/material/Stack";
import {formatBytes} from "../utils/Format";
import {isNil} from "lodash";

export default function DiskOverview(props) {
    const {
        partitionTableType,
        parts,
        showUnallocated
    } = props
    
    const partColor = (part) => {
        switch (part.partType) {
            case "amiga":
                return '#008000'
            case "disk":
                return '#800080'
            case "PartitionTable":
                return '#6060ff'
            case "Partition":
                return '#50ff50'
            case "Unallocated":
                return '#808080'
            default:
                return '#ffff00'
        }
    }

    const formatPartitionTableType = (partitionTableType) => {
        switch (partitionTableType) {
            case 'GuidPartitionTable':
                return 'Guid Partition Table'
            case 'MasterBootRecord':
                return 'Master Boot Record'
            case 'RigidDiskBlock':
                return 'Rigid Disk Block'
            default:
                return ''
        }
    }
    
    const renderLayout = (parts) => {
        const filteredParts = showUnallocated ? parts : parts.filter(x => x.partType !== 'Unallocated')
        return (
            <table style={{
                width: '100%',
                height: '100%'
            }}>
                <tbody>
                <tr style={{
                    height: '100%'
                }}>
                    {filteredParts.map((part, partIndex) => {
                        return (
                            <td
                                key={partIndex}
                                width={`${(part.percentSize === 0 ? 1 : part.percentSize)}%`}
                                style={{height: '100%'}}
                            >
                                <Tooltip arrow={true} title={`${isNil(part.partitionNumber) ? '' : '#' + part.partitionNumber + ', '}${part.partType === 'PartitionTable' ? formatPartitionTableType(part.partitionTableType) : part.partitionType}, ${formatBytes(part.size)}`}>
                                    <div style={{
                                        border: `4px solid ${partColor(part)}`,
                                        backgroundColor: `${partColor(part)}20`,
                                        width: '100%',
                                        height: '100%',
                                        minHeight: '60px',
                                        textAlign: 'center'
                                    }}>
                                        {part.type === 'amiga' && (
                                            <Stack direction="column">
                                                <span>{part.name}</span>
                                                <span>{formatBytes(part.size)}</span>
                                            </Stack>
                                        )}
                                    </div>
                                </Tooltip>
                            </td>
                        )}
                    )}
                </tr>
                </tbody>
            </table>
        )
    }

    const renderList = (parts) => {
        const filteredParts = showUnallocated ? parts : parts.filter(x => x.partType !== 'Unallocated')
        return filteredParts.map((part, partIndex) => {
            return (
                <TableRow key={partIndex}>
                    <TableCell>
                        <Stack direction="row">
                            <div style={{
                                border: `4px solid ${partColor(part)}`,
                                backgroundColor: `${partColor(part)}20`,
                                width: '14px',
                                height: '14px',
                                marginRight: '5px'
                            }}>
                            </div>
                            {part.partType === 'PartitionTable' ? formatPartitionTableType(part.partitionTableType) : part.partitionType}
                        </Stack>
                    </TableCell>
                    <TableCell>
                        {part.fileSystem || ''}
                    </TableCell>
                    <TableCell align="right">
                        {part.partitionNumber || ''}
                    </TableCell>
                    <TableCell align="right">
                        {formatBytes(part.size)}
                    </TableCell>
                    <TableCell align="right">
                        {part.startOffset}
                    </TableCell>
                    <TableCell align="right">
                        {part.endOffset}
                    </TableCell>
                    {partitionTableType && (
                        <Fragment>
                            <TableCell align="right">
                                {partitionTableType === 'RigidDiskBlock' ? part.startCylinder : part.startSector}
                            </TableCell>
                            <TableCell align="right">
                                {partitionTableType === 'RigidDiskBlock' ? part.endCylinder : part.endSector}
                            </TableCell>
                        </Fragment>
                    )}
                </TableRow>
            )
        })
    }
    
    return (
        <React.Fragment>
            {renderLayout(parts)}
            <TableContainer component={Paper} sx={{ mt: 1 }}>
                <Table size="small" aria-label="disk parts">
                    <TableHead>
                        <TableRow>
                            <TableCell>
                                Type
                            </TableCell>
                            <TableCell>
                                File system
                            </TableCell>
                            <TableCell align="right">
                                Number
                            </TableCell>
                            <TableCell align="right">
                                Size
                            </TableCell>
                            <TableCell align="right">
                                Start Offset
                            </TableCell>
                            <TableCell align="right">
                                End Offset
                            </TableCell>
                            {partitionTableType && (
                                <Fragment>
                                    <TableCell align="right">
                                        {partitionTableType === 'RigidDiskBlock' ? 'Start Cylinder' : 'Start Sector'}
                                    </TableCell>
                                    <TableCell align="right">
                                        {partitionTableType === 'RigidDiskBlock' ? 'End Cylinder' : 'End Sector'}
                                    </TableCell>
                                </Fragment>
                            )}
                        </TableRow>
                    </TableHead>
                    <TableBody>
                        {renderList(parts)}
                    </TableBody>
                </Table>
            </TableContainer>
        </React.Fragment>
    )
}