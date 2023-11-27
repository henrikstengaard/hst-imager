import React from 'react';
import Snackbar from '@mui/material/Snackbar';
import IconButton from '@mui/material/IconButton';
import {Alert} from "@mui/material";
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import {HubConnectionBuilder} from "@microsoft/signalr";
import {BackendApiStateContext} from "./BackendApiContext";

const initialState = {
    open: false,
    errorMessage: null
}

export default function ErrorSnackBar() {
    const [connection, setConnection] = React.useState(null);
    const [state, setState] = React.useState({...initialState});
    const {
        backendBaseUrl
    } = React.useContext(BackendApiStateContext)

    React.useEffect(() => {
        if (connection) {
            return
        }

        const newConnection = new HubConnectionBuilder()
            .withUrl(`${backendBaseUrl}hubs/error`)
            .withAutomaticReconnect()
            .build();

        newConnection.on("UpdateError", error => {
            setState({
                ...state,
                open: true,
                errorMessage: error.message
            })
        })

        newConnection.start();

        setConnection(newConnection);
        
        return () => {
            if (!connection) {
                return
            }

            connection.stop();
        };
    }, [backendBaseUrl, connection, setConnection, state])
    
    const handleClose = (event, reason) => {
        if (reason === 'clickaway') {
            return;
        }

        setState({
            ...state,
            open: false
        });
    };
    
    const action = (
        <IconButton
            size="small"
            aria-label="close"
            color="inherit"
            onClick={() => handleClose()}
        >
            <FontAwesomeIcon icon="times" />
        </IconButton>
    );
    
    const {
        open,
        errorMessage
    } = state
    
    return (
        <Snackbar
            anchorOrigin={{
                vertical: 'bottom',
                horizontal: 'right'
        }}
            open={open}
            autoHideDuration={5000}
            onClose={() => handleClose()}
            action={action}
        >
            <Alert onClose={() => handleClose()} severity="error" sx={{ width: '100%' }}>
                {errorMessage}
            </Alert>          
        </Snackbar>
    )
}