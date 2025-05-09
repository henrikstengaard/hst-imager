import { styled } from '@mui/material/styles';
import {useHistory} from "react-router-dom";
import Box from "@mui/material/Box";
import Grid from "@mui/material/Grid";
import Card from "@mui/material/Card";
import CardActionArea from "@mui/material/CardActionArea";
import CardContent from "@mui/material/CardContent";
import Typography from "@mui/material/Typography";
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import React from "react";

const StyledCard = styled(Card)(({theme}) => ({
    height: '100%'
}));


export default function Start() {
    const history = useHistory()

    const actions = [{
        title: 'Read',
        description: 'Read an image file, a physical disk or part of to an image file.',
        url: '/read',
        icon: 'upload'
    },{
        title: 'Write',
        description: 'Write an image file or part of to an image file or a physical disk.',
        url: '/write',
        icon: 'download'
    },{
        title: 'Info',
        description: 'Display information about an image file or a physical disk.',
        url: '/info',
        icon: 'info'
    },{
        title: 'Convert',
        description: 'Convert an image file from one format to another.',
        url: '/convert',
        icon: 'exchange-alt'
    },{
        title: 'Compare',
        description: 'Compare an image file and a physical disk byte by byte.',
        url: '/compare',
        icon: 'check'
    },{
        title: 'Blank',
        description: 'Create a blank image file.',
        url: '/blank',
        icon: 'plus'
    },{
        title: 'Optimize',
        description: 'Optimize an image file.',
        url: '/optimize',
        icon: 'compress'
    }, {
        title: 'Format',
        description: 'Format an image file or a physical disk.',
        url: '/format',
        icon: 'eraser'
    // },{
    //     title: 'Partition',
    //     description: 'Edit partition table for physical disk or image file.',
    //     url: '/partition',
    //     icon: 'hdd'
    }]
    
    return (
        <Box sx={{ m: 1 }}>
            <Grid container spacing={1}>
                {actions.map((action, index) => (
                    <Grid key={index} item xs={6} lg={6}>
                        <StyledCard>
                            <CardActionArea onClick={() => history.push(action.url)} disableRipple>
                                <CardContent>
                                    <Grid container alignItems="center" spacing={2}>
                                        <Grid item>
                                            <Typography variant="h3">
                                                {action.title}
                                            </Typography>
                                        </Grid>
                                        <Grid item>
                                            <FontAwesomeIcon icon={action.icon} size="2x" style={{verticalAlign: 'text-top' }} />
                                        </Grid>
                                    </Grid>
                                    <Typography>
                                        {action.description}
                                    </Typography>
                                </CardContent>
                            </CardActionArea>
                        </StyledCard>
                    </Grid>
                ))}
            </Grid>
        </Box>
    )
}