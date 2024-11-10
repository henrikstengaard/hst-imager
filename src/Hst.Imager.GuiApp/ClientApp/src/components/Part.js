import React from 'react'
import {get} from "lodash";
import {TableHead} from "@mui/material";
import Paper from "@mui/material/Paper";
import TableContainer from "@mui/material/TableContainer";
import Table from "@mui/material/Table";
import TableRow from "@mui/material/TableRow";
import TableCell from "@mui/material/TableCell";
import TableBody from "@mui/material/TableBody";

const renderListFields = (fields) => {
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

const renderListPart = (listPart) => {
    return (
        <TableContainer>
            <Table size="small" aria-label={listPart.title}>
                <TableBody>
                    {renderListFields(listPart.fields)}
                </TableBody>
            </Table>
        </TableContainer>
    )
}

const renderTableHeaderCells = (columns) => {
    return columns.map((column, index) => {
        return (
            <TableCell title={get(column, 'description') || ''} align={column.align} key={index}>
                {column.name}
            </TableCell>
        )}
    )
}

const renderTableRowCells = (aligns, row) => {
    let attributes = {}
    const colSpan = get(row, 'colspan')
    if (colSpan) {
        attributes.colSpan = colSpan
    }
    return row.values.map((value, index) => {
        return (
            <TableCell {...attributes} align={aligns[index]} key={index}>
                {value}
            </TableCell>
        )}
    )
}

const renderTablePart = (tablePart) => {
    const columnAligns = tablePart.columns.map(column => get(column, 'align') ?? 'left')
    return (
        <TableContainer>
            <Table size="small" aria-label={tablePart.title}>
                <TableHead>
                    <TableRow>
                        {renderTableHeaderCells(tablePart.columns)}
                    </TableRow>
                </TableHead>
                <TableBody>
                    {tablePart.rows.map((row, index) => {
                        return (
                            <TableRow key={index}>
                                {renderTableRowCells(columnAligns, row)}
                            </TableRow>
                        )}
                    )}
                </TableBody>
            </Table>
        </TableContainer>
    )
}

export default function Part(props) {
    const {
        part
    } = props

    if (!part) {
        return null
    }
    
    const renderPart = (part) => {
        switch(part.type) {
            case 'list':
                return renderListPart(part)
            case 'table':
                return renderTablePart(part)
            default:
                return null
        }
    }

    return (
        <Paper sx={{ width: '100%' }}>
            {renderPart(part)}
        </Paper>
    )
}