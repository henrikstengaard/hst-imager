import React, {Fragment} from "react";
import TableContainer from "@mui/material/TableContainer";
import Paper from "@mui/material/Paper";
import Table from "@mui/material/Table";
import {TableHead} from "@mui/material";
import TableRow from "@mui/material/TableRow";
import TableCell from "@mui/material/TableCell";
import TableBody from "@mui/material/TableBody";
import Stack from "@mui/material/Stack";
import {formatBytes} from "../utils/Format";

export default function DiskOverview(props) {
    const {
        partitionTableType,
        parts
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
    
    const formatPartType = (partType) => {
        switch (partType) {
            case 'PartitionTable':
                return 'Partition Table'
            case 'Partition':
                return 'Partition'
            case 'Unallocated':
                return 'Unallocated'
            default:
                return ''
        }
    }

    const renderLayout = (parts) => {
        return (
            <table style={{
                width: '100%',
                height: '100%'
            }}>
                <tbody>
                <tr style={{
                    height: '100%'
                }}>
                    {parts.map((part, partIndex) => {
                        return (
                            <td
                                key={partIndex}
                                width={`${part.percentSize}%`}
                                style={{height: '100%'}}
                            >
                                <div style={{
                                    border: `4px solid ${partColor(part)}`,
                                    backgroundColor: `${partColor(part)}20`,
                                    width: '100%',
                                    minWidth: part.type === 'amiga' ? '100px' : null,
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
                            </td>
                        )}
                    )}
                </tr>
                </tbody>
            </table>
        )
    }

    const renderList = (parts) => {
        return parts.map((part, partIndex) => {
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
                            {part.fileSystem}
                        </Stack>
                    </TableCell>
                    <TableCell>
                        {formatPartitionTableType(part.partitionTableType)}
                    </TableCell>
                    <TableCell>
                        {formatPartType(part.partType)}
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
                                {partitionTableType === 'RigidDiskBlock' ? part.startCylinder : part.startOffset}
                            </TableCell>
                            <TableCell align="right">
                                {partitionTableType === 'RigidDiskBlock' ? part.endCylinder : part.endOffset}
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
                                File system
                            </TableCell>
                            <TableCell>
                                Partition table
                            </TableCell>
                            <TableCell>
                                Type
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