import { get, isNil } from 'lodash'
import React from "react";
import {AppStateContext} from "./AppStateContext";
import IconButton from "@mui/material/IconButton";
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import {HubConnectionBuilder} from "@microsoft/signalr";

export default function BrowseOpenDialog(props) {
    const {
        id,
        title = 'Open file',
        fileFilters = [{
            name: 'Image files',
            extensions: ['img', 'hdf', 'vhd']
        }, {
            name: 'All files',
            extensions: ['*']
        }],
        onChange
    } = props

    const appState = React.useContext(AppStateContext)
    const [ connection, setConnection ] = React.useState(null);

    // setup signalr connection and listeners
    React.useEffect(() => {
        if (connection) {
            return
        }

        const newConnection = new HubConnectionBuilder()
            .withUrl('/hubs/show-dialog-result')
            .withAutomaticReconnect()
            .build();

        try {
            newConnection
                .start()
                .then(() => {
                    newConnection.on('ShowDialogResult', showDialogResult => {
                        if (get(showDialogResult, 'isSuccess') !== true || get(showDialogResult, 'id') !== id) {
                            return
                        }

                        if (isNil(onChange)) {
                            return
                        }

                        onChange(showDialogResult.paths[0])
                    });
                })
                .catch((err) => {
                    console.error(`Error: ${err}`)
                })
        } catch (error) {
            console.error(error)
        }

        setConnection(newConnection)

        return () => {
            if (!connection) {
                return
            }
            connection.stop();
        };
    }, [connection, id, onChange])
    
    const handleBrowseClick = async () => {
        if (!appState || !appState.isElectronActive)
        {
            console.error('Browse open dialog is only available with Electron')
            return
        }

        const response = await fetch('api/show-open-dialog', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                id,
                title,
                fileFilters
            })
        });
        if (!response.ok) {
            console.error('Failed to show open dialog')
        }
    }

    return (
        <IconButton aria-label="browse" disableRipple onClick={async () => await handleBrowseClick()}>
            <FontAwesomeIcon icon="ellipsis-h"/>
        </IconButton>
    )
}