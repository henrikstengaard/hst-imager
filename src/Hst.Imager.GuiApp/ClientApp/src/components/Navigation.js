import React from 'react'
import {useHistory} from 'react-router-dom'
import { styled } from '@mui/material/styles';
import Box from '@mui/material/Box';
import MuiDrawer from '@mui/material/Drawer';
import List from '@mui/material/List';
// import CssBaseline from '@mui/material/CssBaseline';
// import Toolbar from '@mui/material/Toolbar';
// import Divider from '@mui/material/Divider';
// import IconButton from '@mui/material/IconButton';
// import MenuIcon from '@mui/icons-material/Menu';
// import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
// import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import ListItem from '@mui/material/ListItem';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
// import InboxIcon from '@mui/icons-material/MoveToInbox';
// import MailIcon from '@mui/icons-material/Mail';
import {FontAwesomeIcon} from '@fortawesome/react-fontawesome'
import {AppStateContext} from "./AppStateContext";
import {get} from "lodash";

const drawerOpenWidth = 180;
const drawerClosedWidth = 62;

const openedMixin = (theme) => ({
    width: drawerOpenWidth,
    transition: theme.transitions.create('width', {
        easing: theme.transitions.easing.sharp,
        duration: theme.transitions.duration.enteringScreen,
    }),
    overflowX: 'hidden',
});

const closedMixin = (theme) => ({
    transition: theme.transitions.create('width', {
        easing: theme.transitions.easing.sharp,
        duration: theme.transitions.duration.shortest,
    }),
    overflowX: 'hidden',
    // width: `calc(${theme.spacing(7)} + 1px)`,
    // [theme.breakpoints.up('sm')]: {
    //     width: `calc(${theme.spacing(9)} + 1px)`,
    // },
});

const Drawer = styled(MuiDrawer, { shouldForwardProp: (prop) => prop !== 'open' })(
    ({ theme, open }) => ({
        width: drawerOpenWidth,
        flexShrink: 0,
        whiteSpace: 'nowrap',
        boxSizing: 'border-box',
        ...(open && {
            ...openedMixin(theme),
            '& .MuiDrawer-paper': openedMixin(theme),
        }),
        ...(!open && {
            ...closedMixin(theme),
            '& .MuiDrawer-paper': closedMixin(theme),
        }),
    }),
);

const Content = styled(Box)(
    ({ theme, width}) => ({
        width: `calc(100% - ${width})`,
        transition: theme.transitions.create('width', {
            easing: theme.transitions.easing.sharp,
            duration: theme.transitions.duration.enteringScreen,
        })
    }),
);

export default function Navigation(props) {
    const {
        children
    } = props
    
    const history = useHistory()
    const [open, setOpen] = React.useState(false);
    const appState = React.useContext(AppStateContext)
    
    const hasTitleBar = get(appState, 'hasTitleBar') || false

    const items = [
        {
            text: 'Start',
            icon: 'home',
            path: '/'
        },
        {
            text: 'Read',
            icon: 'upload',
            path: '/read'
        },
        {
            text: 'Write',
            icon: 'download',
            path: '/write'
        },
        {
            text: 'Info',
            icon: 'info',
            path: '/info'
        },
        {
            text: 'Convert',
            icon: 'exchange-alt',
            path: '/convert'
        },
        {
            text: 'Compare',
            icon: 'check',
            path: '/compare'
        },
        {
            text: 'Blank',
            icon: 'plus',
            path: '/blank'
        },
        {
            text: 'Optimize',
            icon: 'compress',
            path: '/optimize'
        },
        {
            text: 'Format',
            icon: 'eraser',
            path: '/format'
        },
        // {
        //     text: 'Partition',
        //     icon: 'hdd',
        //     path: '/partition'
        // },
        {
            text: 'Settings',
            icon: 'cog',
            path: '/settings'
        },
        {
            text: 'About',
            icon: 'question',
            path: '/about'
        }
    ]
    
    const handleOpen = () => {
        setOpen(!open)
    }
    
    const handleRedirect = (path) => history.push(path)
    const width = `${open ? drawerOpenWidth : drawerClosedWidth}px`
    
    return (
        <React.Fragment>
            <Drawer position="fixed" open={open} variant="permanent"
                    sx={{
                        width,
                        overflowX: 'hidden',
                        [`& .MuiDrawer-paper`]: { width, boxSizing: 'border-box' },
                    }}>
                <List sx={{marginTop: hasTitleBar ? '32px' : '0'}}>
                    {items.map((item, index) => (
                        <ListItem button key={index} onClick={() => handleRedirect(item.path)}>
                            <ListItemIcon>
                                <FontAwesomeIcon
                                    icon={item.icon}
                                    style={{ minWidth: '18px' }}
                                />
                            </ListItemIcon>
                            <ListItemText primary={item.text} />
                        </ListItem>
                    ))}
                </List>
                <Box sx={{flexGrow: 1 }} />
                <List >
                    <ListItem button onClick={() => handleOpen()} sx={{ width: '100%' }}>
                        <ListItemIcon>
                            <FontAwesomeIcon
                                icon={open ? 'chevron-left' : 'chevron-right'}
                                style={{ minWidth: '18px' }}
                            />
                        </ListItemIcon>
                    </ListItem>
                </List>
            </Drawer>
            <Content width={width}>
                {children} 
            </Content>
        </React.Fragment>
    )
}