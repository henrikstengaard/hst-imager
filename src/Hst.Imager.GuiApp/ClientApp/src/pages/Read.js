import {get, isNil} from 'lodash'
import React from 'react'
import Box from '@mui/material/Box'
import Grid from '@mui/material/Grid'
import Stack from '@mui/material/Stack'
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome"
import Media from '../components/Media'
import Button from '../components/Button'
import BrowseSaveDialog from "../components/BrowseSaveDialog";
import Title from "../components/Title";
import TextField from '../components/TextField'
import RedirectButton from "../components/RedirectButton";
import MediaSelectField from "../components/MediaSelectField";
import ConfirmDialog from "../components/ConfirmDialog";
import {Api} from "../utils/Api";
import {HubConnectionBuilder} from "@microsoft/signalr";
import Typography from "@mui/material/Typography";

export default function Read() {
    const [confirmOpen, setConfirmOpen] = React.useState(false);
    const [sourceMedia, setSourceMedia] = React.useState(null)
    const [medias, setMedias] = React.useState(null)
    const [destinationPath, setDestinationPath] = React.useState(null)
    const [connection, setConnection] = React.useState(null);

    const api = React.useMemo(() => new Api(), []);

    const getPath = ({medias, path}) => {
        if (medias === null || medias.length === 0) {
            return null
        }

        if (path === null || path === undefined) {
            return medias[0].path
        }

        const media = medias.find(x => x.path === path)
        return media === null ? medias[0].path : media.path
    }

    const handleGetMedias = React.useCallback(async () => {
        async function getMedias() {
            await api.list()
        }
        await getMedias()
    }, [api])

    const getInfo = async (path) => {
        const response = await fetch('api/info', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                sourceType: 'PhysicalDisk',
                path
            })
        });
        if (!response.ok) {
            console.error('Failed to get info')
        }
    }
    
    // get medias
    React.useEffect(async () => {
        if (medias !== null) {
            return
        }
        await handleGetMedias()
    }, [medias, handleGetMedias])

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
                        setSourceMedia(media)
                    });

                    newConnection.on('List', async (medias) => {
                        const newPath = getPath({medias: medias, path: get(sourceMedia, 'path')})
                        const newMedia = newPath ? medias.find(x => x.path === newPath) : null

                        setMedias(medias || [])
                        if (newMedia) {
                            setSourceMedia(newMedia)
                            await getInfo(newMedia.path)
                        }
                    })
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
    }, [connection, getPath, setMedias, setSourceMedia, sourceMedia])
    
    const handleRead = async () => {
        const response = await fetch('api/read', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                title: `Reading disk '${sourceMedia.name}' to file '${destinationPath}'`,
                sourcePath: sourceMedia.path,
                destinationPath
            })
        });
        if (!response.ok) {
            console.error('Failed to read')
        }
    }

    const handleConfirm = async (confirmed) => {
        setConfirmOpen(false)
        if (!confirmed) {
            return
        }
        await handleRead()
    }

    const handleUpdate = async () => {
        await api.list()
    }

    const handleCancel = () => {
        setConfirmOpen(false)
        setSourceMedia(null)
        setMedias(null)
        setDestinationPath(null)
        setConnection(null)
    }
    
    const readDisabled = isNil(sourceMedia) || isNil(destinationPath)

    return (
        <Box>
            <ConfirmDialog
                id="confirm-read"
                open={confirmOpen}
                title="Read"
                description={`Do you want to read disk '${sourceMedia === null ? '' : sourceMedia.name}' to file '${destinationPath}'?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />
            <Title
                text="Read"
                description="Read physical disk to image file."
            />
            <Grid container spacing="2" direction="row" alignItems="center" sx={{mt: 2}}>
                <Grid item xs={12} lg={6}>
                    <MediaSelectField
                        label={
                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                <FontAwesomeIcon icon="hdd" style={{marginRight: '5px'}} /> Source disk
                            </div>
                        }
                        id="source-disk"
                        medias={medias || []}
                        path={get(sourceMedia, 'path') || ''}
                        onChange={(media) => setSourceMedia(media)}
                    />
                </Grid>
            </Grid>
            <Grid container spacing="2" direction="row" alignItems="center" sx={{mt: 2}}>
                <Grid item xs={12} lg={6}>
                    <TextField
                        id="destination-file"
                        label={
                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                <FontAwesomeIcon icon="file" style={{marginRight: '5px'}} /> Destination file
                            </div>
                        }
                        value={destinationPath || ''}
                        endAdornment={
                            <BrowseSaveDialog
                                id="read-destination-path"
                                title="Select destination image"
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
            <Grid container spacing="2" direction="row" alignItems="center" sx={{mt: 2}}>
                <Grid item xs={12} lg={6}>
                    <Box display="flex" justifyContent="flex-end">
                        <Stack direction="row" spacing={2} sx={{mt: 2}}>
                            <RedirectButton
                                path="/"
                                icon="ban"
                                onClick={async () => handleCancel()}
                            >
                                Cancel
                            </RedirectButton>
                            <Button
                                icon="sync-alt"
                                onClick={async () => handleUpdate()}
                            >
                                Update
                            </Button>
                            <Button
                                disabled={readDisabled}
                                icon="upload"
                                onClick={async () => setConfirmOpen(true)}
                            >
                                Start read
                            </Button>
                        </Stack>
                    </Box>
                </Grid>
            </Grid>
            {get(sourceMedia, 'diskInfo') && (
                <Grid container spacing="2" direction="row" alignItems="center" sx={{mt: 2}}>
                    <Grid item xs={12}>
                        <Typography variant="h3">
                            Source disk
                        </Typography>
                        <Typography>
                            Disk information read from source disk.
                        </Typography>
                        <Media media={sourceMedia}/>
                    </Grid>
                </Grid>
            )}
        </Box>
    )
}