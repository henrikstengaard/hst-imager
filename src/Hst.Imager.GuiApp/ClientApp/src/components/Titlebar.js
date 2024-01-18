import React from 'react'
import Box from '@mui/material/Box'
import AppBar from '@mui/material/AppBar'
import Toolbar from '@mui/material/Toolbar'
import Typography from '@mui/material/Typography'
import IconButton from '@mui/material/IconButton'
import {FontAwesomeIcon} from '@fortawesome/react-fontawesome'
import {HST_IMAGER_VERSION} from '../Constants'
import {AppStateContext} from "./AppStateContext";

export default function Titlebar() {
    const appState = React.useContext(AppStateContext)
    const [maximized] = React.useState(false)

    const handleMinimizeWindow = () => {
        appState.hostIpc.minimizeWindow()
    }

    const handleMaximizeWindow = () => {
        appState.hostIpc.maximizeWindow()
    }

    const handleRestoreWindow = () => {
        appState.hostIpc.restoreWindow()
    }

    const handleCloseWindow = () => {
        appState.hostIpc.closeWindow()
    }

    return (
        <AppBar
            id="titlebar"
            position="fixed"
            sx={{
                zIndex: (theme) => theme.zIndex.drawer + 10000,
                WebkitAppRegion: 'drag',
                userSelect: 'none'
            }}
        >
            <Toolbar disableGutters style={{minHeight: '32px', padding: '7px'}}>
                <img src="icons/icon-192x192.png" height="28px" alt="Hst Imager app icon"
                     style={{paddingLeft: '2px', paddingRight: '2px'}}/>
                <Typography variant="h1" component="div" sx={{flexGrow: 1}}>
                    Hst Imager v{HST_IMAGER_VERSION}
                </Typography>
                <Box style={{WebkitAppRegion: 'no-drag'}}>
                    <IconButton
                        disableRipple={true}
                        size="small"
                        color="inherit"
                        aria-label="minimize"
                        onClick={() => handleMinimizeWindow()}
                    >
                        <FontAwesomeIcon icon="window-minimize"/>
                    </IconButton>
                    {!maximized && (
                        <IconButton
                            disableRipple={true}
                            size="small"
                            color="inherit"
                            aria-label="maximize"
                            onClick={() => handleMaximizeWindow()}
                        >
                            <FontAwesomeIcon icon="window-maximize"/>
                        </IconButton>
                    )}
                    {maximized && (
                        <IconButton
                            disableRipple={true}
                            size="small"
                            color="inherit"
                            aria-label="restore"
                            onClick={() => handleRestoreWindow()}
                        >
                            <FontAwesomeIcon icon="window-restore"/>
                        </IconButton>
                    )}
                    <IconButton
                        disableRipple={true}
                        size="small"
                        color="inherit"
                        aria-label="close"
                        onClick={() => handleCloseWindow()}
                    >
                        <FontAwesomeIcon icon="window-close"/>
                    </IconButton>
                </Box>
            </Toolbar>
        </AppBar>
    )
}
