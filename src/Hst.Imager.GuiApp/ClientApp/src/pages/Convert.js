import React from 'react'
import {get, isNil} from "lodash";
import Box from "@mui/material/Box";
import Title from "../components/Title";
import Grid from "@mui/material/Grid";
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import TextField from "../components/TextField";
import BrowseSaveDialog from "../components/BrowseSaveDialog";
import Stack from "@mui/material/Stack";
import RedirectButton from "../components/RedirectButton";
import Button from "../components/Button";
import BrowseOpenDialog from "../components/BrowseOpenDialog";
import ConfirmDialog from "../components/ConfirmDialog";
import Typography from "@mui/material/Typography";
import Media from "../components/Media";
import {HubConnectionBuilder} from "@microsoft/signalr";

export default function Convert() {
    const [confirmOpen, setConfirmOpen] = React.useState(false)
    const [media, setMedia] = React.useState(null)
    const [sourcePath, setSourcePath] = React.useState(null)
    const [destinationPath, setDestinationPath] = React.useState(null)
    const [connection, setConnection] = React.useState(null)

    const handleCancel = () => {
        setConfirmOpen(false)
        setMedia(null)
        setSourcePath(null)
        setDestinationPath(null)
        setConnection(null)
    }

    // setup signalr connection and listeners
    React.useEffect(() => {
        if (connection) {
            return
        }

        const newConnection = new HubConnectionBuilder()
            .withUrl('/hubs/result')
            .withAutomaticReconnect()
            .build();

        try {
            newConnection
                .start()
                .then(() => {
                    newConnection.on("Info", (media) => {
                        setMedia(media)
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
    }, [connection, setMedia])
    
    const getInfo = async (path) => {
        const response = await fetch('api/info', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                sourceType: 'ImageFile',
                path
            })
        });
        if (!response.ok) {
            console.error('Failed to get info')
        }
    }
    
    const handleConvert = async () => {
        const response = await fetch('api/convert', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                title: `Converting file '${sourcePath}' to file '${destinationPath}'`,
                sourcePath,
                destinationPath
            })
        });
        if (!response.ok) {
            console.error('Failed to convert')
        }
    }
    
    const handleConfirm = async (confirmed) => {
        setConfirmOpen(false)
        if (!confirmed) {
            return
        }
        await handleConvert()
    }

    const convertDisabled = isNil(sourcePath) || isNil(destinationPath)

    return (
        <Box>
            <ConfirmDialog
                id="confirm-convert"
                open={confirmOpen}
                title="Convert"
                description={`Do you want to convert file '${sourcePath}' to file '${destinationPath}'?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />            
            <Title
                text="Convert"
                description="Convert image file from one format to another."
            />
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12} lg={6}>
                    <TextField
                        id="source-path"
                        label={
                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                <FontAwesomeIcon icon="file" style={{marginRight: '5px'}} /> Source image file
                            </div>
                        }
                        value={sourcePath || ''}
                        endAdornment={
                            <BrowseOpenDialog
                                id="browse-source-path"
                                title="Select source image file"
                                onChange={(path) => setSourcePath(path)}
                            />
                        }
                        onChange={(event) => {
                            setSourcePath(get(event, 'target.value'))
                            if (media) {
                                setMedia(null)
                            }
                        }}
                        onKeyDown={async (event) => {
                            if (event.key !== 'Enter') {
                                return
                            }
                            await getInfo(sourcePath)
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12} lg={6}>
                    <TextField
                        id="destination-path"
                        label={
                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                <FontAwesomeIcon icon="file" style={{marginRight: '5px'}} /> Destination image file
                            </div>
                        }
                        value={destinationPath || ''}
                        endAdornment={
                            <BrowseSaveDialog
                                id="browse-destination-path"
                                title="Select destination image file"
                                onChange={(path) => setDestinationPath(path)}
                            />
                        }
                        onChange={(event) => setDestinationPath(get(event, 'target.value'))}
                        onKeyDown={async (event) => {
                            if (event.key !== 'Enter') {
                                return
                            }
                            setConfirmOpen(true)
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12} lg={6}>
                    <Box display="flex" justifyContent="flex-end">
                        <Stack direction="row" spacing={2} sx={{mt: 1}}>
                            <RedirectButton
                                path="/"
                                icon="ban"
                                onClick={async () => handleCancel()}
                            >
                                Cancel
                            </RedirectButton>
                            <Button
                                disabled={convertDisabled}
                                icon="exchange-alt"
                                onClick={async () => setConfirmOpen(true)}
                            >
                                Start convert
                            </Button>
                        </Stack>
                    </Box>
                </Grid>
            </Grid>
            {media && media.diskInfo && (
                <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                    <Grid item xs={12}>
                        <Typography variant="h3">
                            Source file
                        </Typography>
                        <Typography>
                            Disk information read from source file.
                        </Typography>
                        <Media media={media}/>
                    </Grid>
                </Grid>
            )}
        </Box>
    )
}