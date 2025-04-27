import * as React from 'react';
import MuiAccordion from '@mui/material/Accordion';
import AccordionDetails from '@mui/material/AccordionDetails';
import AccordionSummary from '@mui/material/AccordionSummary';
import Typography from '@mui/material/Typography';
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import {styled} from "@mui/material/styles";
import Stack from "@mui/material/Stack";

const StyledAccordionSummary = styled(AccordionSummary)(({theme}) => ({
    color: theme.palette.primary.main
}));

export default function Accordion(props) {
    const {
        children,
        expanded: initialExpanded = true,
        border = true,
        id,
        title,
        icon
    } = props

    const [expanded, setExpanded] = React.useState(initialExpanded);

    const handleChange = () => {
        setExpanded(!expanded)
    }
    
    const renderDetails = (children) => (
        <AccordionDetails>
            {children}
        </AccordionDetails>
    )
    
    return (
        <MuiAccordion
            elevation={border ? 1 : 0}
            expanded={expanded}
            disableGutters={true}
            onChange={() => handleChange()}
        >
            <StyledAccordionSummary
                expandIcon={<FontAwesomeIcon icon="chevron-down"/>}
                aria-controls={`${id}-content`}
                id={`${id}-header`}
            >
                <Stack direction="row" alignItems="center" justifyContent="space-between">
                    {icon && <FontAwesomeIcon icon={icon} style={{marginRight: 5}} />}
                    <Typography>
                        {title}
                    </Typography>
                </Stack>
            </StyledAccordionSummary>
            {border ? renderDetails(children) : children}
        </MuiAccordion>
    );
}