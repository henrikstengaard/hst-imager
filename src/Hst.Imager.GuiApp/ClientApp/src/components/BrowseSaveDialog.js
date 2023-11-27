import { get, isNil } from 'lodash'
import React from "react";
import {AppStateContext} from "./AppStateContext";
import IconButton from "@mui/material/IconButton";
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import {HubConnectionBuilder} from "@microsoft/signalr";
import {BackendApiStateContext} from "./BackendApiContext";

export default function BrowseSaveDialog(props) {
    const {
        id,
        title = 'Save file',
        fileFilters = [{
            name: 'Image files',
            extensions: ['img', 'hdf']
        }, {
            name: 'Virtual hard disk',
            extensions: ['vhd']
        }, {
            name: 'All files',
            extensions: ['*']
        }],
        onChange
    } = props

    const [connection, setConnection] = React.useState(null);
    const appState = React.useContext(AppStateContext)
    const {
        backendBaseUrl
    } = React.useContext(BackendApiStateContext)
    
    React.useEffect(() => {
        if (connection) {
            return
        }
        
        const newConnection = new HubConnectionBuilder()
            .withUrl(`${backendBaseUrl}hubs/show-dialog-result`)
            .withAutomaticReconnect()
            .build();

        newConnection.on("ShowDialogResult", showDialogResult => {
            if (get(showDialogResult, 'isSuccess') !== true || get(showDialogResult, 'id') !== id) {
                return
            }

            if (isNil(onChange)) {
                return
            }

            onChange(showDialogResult.paths[0])
        })

        newConnection.start();

        setConnection(newConnection);
        
        return () => {
            if (!connection) {
                return
            }

            connection.stop();
        };
    }, [backendBaseUrl, connection, id, onChange, setConnection])
    
    const handleBrowseClick = async () => {
        appState.hostIpc.showSaveDialog({ id, title, filters: fileFilters })
    }
    
    return (
        <IconButton aria-label="browse" disableRipple onClick={async () => await handleBrowseClick()}>
            <FontAwesomeIcon icon="ellipsis-h"/>
        </IconButton>
    )
}