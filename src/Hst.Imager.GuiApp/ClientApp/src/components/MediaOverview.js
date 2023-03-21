import {formatBytes} from "../utils/Format";
import React, {Fragment} from "react";
import TableContainer from "@mui/material/TableContainer";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import Paper from "@mui/material/Paper";
import TableRow from "@mui/material/TableRow";
import TableCell from "@mui/material/TableCell";
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import Typography from "@mui/material/Typography";
import {get, set} from "lodash";
import {styled} from "@mui/material/styles";
import AccordionSummary from "@mui/material/AccordionSummary";
import DiskOverview from "./DiskOverview";
import CheckboxField from "./CheckboxField";

const StyledAccordionSummary = styled(AccordionSummary)(({theme}) => ({
    padding: 0,
    color: theme.palette.primary.main
}));

const initialState = {
    diskExpanded: true,
    gptExpanded: true,
    mbrExpanded: true,
    rdbExpanded: true,
    showUnallocated: true
}

export default function MediaOverview(props) {
    const {
        media
    } = props

    const [state, setState] = React.useState({...initialState});

    const handleChange = ({ name, value }) => {
        set(state, name, value)
        setState({...state})
    }
    
    const {
        diskExpanded,
        gptExpanded,
        mbrExpanded,
        rdbExpanded,
        showUnallocated
    } = state

    const diskParts = get(media, 'diskInfo.diskParts')
    const gptPartitionTablePart = get(media, 'diskInfo.gptPartitionTablePart')
    const mbrPartitionTablePart = get(media, 'diskInfo.mbrPartitionTablePart')
    const rdbPartitionTablePart = get(media, 'diskInfo.rdbPartitionTablePart')
    
    return (
        <Fragment>
            <CheckboxField
                id="show-unallocated"
                label="Show unallocated"
                value={showUnallocated}
                onChange={(checked) => handleChange({ name: 'showUnallocated', value: checked})}
            />
            <TableContainer component={Paper}>
                <Table size="small" aria-label="media details">
                    <TableBody>
                        <TableRow>
                            <TableCell>
                                <StyledAccordionSummary
                                    expandIcon={<FontAwesomeIcon icon={diskExpanded ? 'chevron-up' : 'chevron-down'}/>}
                                    onClick={() => handleChange({ name: 'diskExpanded', value: !diskExpanded})}
                                >
                                    <Typography>
                                        {`Disk: ${media.name}, ${formatBytes(media.diskSize)}`}
                                    </Typography>
                                </StyledAccordionSummary>
                            </TableCell>
                        </TableRow>
                        {diskExpanded && (
                            <TableRow>
                                <TableCell>
                                    <DiskOverview parts={diskParts} showUnallocated={showUnallocated} />
                                </TableCell>
                            </TableRow>
                        )}
    
                        {gptPartitionTablePart && (
                            <React.Fragment>
                                <TableRow>
                                    <TableCell>
                                        <StyledAccordionSummary
                                            expandIcon={<FontAwesomeIcon icon={gptExpanded ? 'chevron-up' : 'chevron-down'}/>}
                                            onClick={() => handleChange({ name: 'gptExpanded', value: !gptExpanded})}
                                        >
                                            <Typography>
                                                {`Guid Partition Table: ${formatBytes(gptPartitionTablePart.size)}`}
                                            </Typography>
                                        </StyledAccordionSummary>
                                    </TableCell>
                                </TableRow>
                                {gptExpanded && (
                                    <TableRow>
                                        <TableCell>
                                            <DiskOverview
                                                partitionTableType={gptPartitionTablePart.partitionTableType}
                                                parts={gptPartitionTablePart.parts}
                                                showUnallocated={showUnallocated}
                                            />
                                        </TableCell>
                                    </TableRow>
                                )}
                            </React.Fragment>
                        )}
                        
                        {mbrPartitionTablePart && (
                            <React.Fragment>
                                <TableRow>
                                    <TableCell>
                                        <StyledAccordionSummary
                                            expandIcon={<FontAwesomeIcon icon={mbrExpanded ? 'chevron-up' : 'chevron-down'}/>}
                                            onClick={() => handleChange({ name: 'mbrExpanded', value: !mbrExpanded})}
                                        >
                                            <Typography>
                                                {`Master Boot Record: ${formatBytes(mbrPartitionTablePart.size)}`}
                                            </Typography>
                                        </StyledAccordionSummary>
                                    </TableCell>
                                </TableRow>
                                {mbrExpanded && (
                                    <TableRow>
                                        <TableCell>
                                            <DiskOverview
                                                partitionTableType={mbrPartitionTablePart.partitionTableType}
                                                parts={mbrPartitionTablePart.parts}
                                                showUnallocated={showUnallocated}
                                            />
                                        </TableCell>
                                    </TableRow>
                                )}
                            </React.Fragment>                        
                        )}
    
                        {rdbPartitionTablePart && (
                            <React.Fragment>
                                <TableRow>
                                    <TableCell>
                                        <StyledAccordionSummary
                                            expandIcon={<FontAwesomeIcon icon={rdbExpanded ? 'chevron-up' : 'chevron-down'}/>}
                                            onClick={() => handleChange({ name: 'rdbExpanded', value: !rdbExpanded})}
                                        >
                                            <Typography>
                                                {`Rigid Disk Block: ${formatBytes(rdbPartitionTablePart.size)}`}
                                            </Typography>
                                        </StyledAccordionSummary>
                                    </TableCell>
                                </TableRow>
                                {rdbExpanded && (
                                    <TableRow>
                                        <TableCell>
                                            <DiskOverview
                                                partitionTableType={rdbPartitionTablePart.partitionTableType}
                                                parts={rdbPartitionTablePart.parts}
                                                showUnallocated={showUnallocated}
                                            />
                                        </TableCell>
                                    </TableRow>
                                )}
                            </React.Fragment>
                        )}
                    </TableBody>
                </Table>
            </TableContainer>
        </Fragment>
    )
}