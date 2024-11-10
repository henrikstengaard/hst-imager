import * as React from 'react';
import MuiAccordion from '@mui/material/Accordion';
import AccordionDetails from '@mui/material/AccordionDetails';
import AccordionSummary from '@mui/material/AccordionSummary';
import Typography from '@mui/material/Typography';
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import {styled} from "@mui/material/styles";

const StyledAccordionSummary = styled(AccordionSummary)(({theme}) => ({
    color: theme.palette.primary.main
}));

export default function Accordion(props) {
    const {
        children,
        expanded: initialExpanded = true,
        id,
        title
    } = props

    const [expanded, setExpanded] = React.useState(initialExpanded);

    const handleChange = () => {
        setExpanded(!expanded)
    }
    
    return (
        <MuiAccordion expanded={expanded} disableGutters={true} onChange={() => handleChange()}>
            <StyledAccordionSummary
                expandIcon={<FontAwesomeIcon icon="chevron-down"/>}
                aria-controls={`${id}-content`}
                id={`${id}-header`}
            >
                <Typography>
                    {title}
                </Typography>
            </StyledAccordionSummary>
            <AccordionDetails>
                {children}
            </AccordionDetails>
        </MuiAccordion>
    );
}