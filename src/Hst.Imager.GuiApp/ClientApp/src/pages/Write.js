import React from 'react'
import Title from "../components/Title";
import Box from "@mui/material/Box";
import {get, isNil} from "lodash";
import Grid from "@mui/material/Grid";
import MediaSelectField from "../components/MediaSelectField";
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import TextField from "../components/TextField";
import Stack from "@mui/material/Stack";
import RedirectButton from "../components/RedirectButton";
import Button from "../components/Button";
import BrowseOpenDialog from "../components/BrowseOpenDialog";
import ConfirmDialog from "../components/ConfirmDialog";
import {Api} from "../utils/Api";
import {HubConnectionBuilder} from "@microsoft/signalr";
import Media from "../components/Media";
import Typography from "@mui/material/Typography";
import CheckboxField from "../components/CheckboxField";

export default function Write() {
    const [confirmOpen, setConfirmOpen] = React.useState(false);
    const [sourceMedia, setSourceMedia] = React.useState(null)
    const [destinationMedia, setDestinationMedia] = React.useState(null)
    const [medias, setMedias] = React.useState(null)
    const [sourcePath, setSourcePath] = React.useState(null)
    const [verify, setVerify] = React.useState(false)
    const [force, setForce] = React.useState(false)
    const [retries, setRetries] = React.useState(5)
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
                sourceType: 'ImageFile',
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
                        const newPath = getPath({medias: medias, path: get(destinationMedia, 'path')})
                        const newMedia = newPath ? medias.find(x => x.path === newPath) : null

                        setMedias(medias)
                        if (newMedia) {
                            setDestinationMedia(newMedia)
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
    }, [connection, destinationMedia, getPath, setMedias, setDestinationMedia, setSourceMedia])
    
    const handleWrite = async () => {
        const response = await fetch('api/write', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                title: `Writing file '${sourcePath}' to disk '${get(destinationMedia, 'name') || ''}'`,
                sourcePath,
                destinationPath: destinationMedia.path,
                retries,
                verify,
                force
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
        await handleWrite()
    }

    const handleUpdate = async () => {
        await api.list()
    }

    const handleCancel = () => {
        if (connection) {
            connection.stop()
        }
        setConfirmOpen(false)
        setSourceMedia(null)
        setDestinationMedia(null)
        setMedias([])
        setSourcePath(null)
        setVerify(false)
        setForce(false)
        setRetries(5)
        setConnection(null)
    }
    
    const writeDisabled = isNil(sourcePath) || isNil(destinationMedia)

    return (
        <Box>
            <ConfirmDialog
                id="confirm-write"
                open={confirmOpen}
                title="Write"
                description={`Do you want to write file '${sourcePath}' to disk '${get(destinationMedia, 'name') || ''}'?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />
            <Title
                text="Write"
                description="Write image file to physical disk."
            />
            <Grid container spacing="2" direction="row" alignItems="center" sx={{mt: 2}}>
                <Grid item xs={12} lg={6}>
                    <TextField
                        id="source-path"
                        label={
                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                <FontAwesomeIcon icon="file" style={{marginRight: '5px'}} /> Source file
                            </div>
                        }
                        value={sourcePath || ''}
                        endAdornment={
                            <BrowseOpenDialog
                                id="browse-source-path"
                                title="Select source image file"
                                onChange={async (path) => {
                                    setSourcePath(path)
                                    await getInfo(path)
                                }}
                            />
                        }
                        onChange={(event) => setSourcePath(get(event, 'target.value'))}
                        onKeyDown={async (event) => {
                            if (event.key !== 'Enter') {
                                return
                            }
                            await getInfo(sourcePath)
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing="2" direction="row" alignItems="center" sx={{mt: 2}}>
                <Grid item xs={12} lg={6}>
                    <MediaSelectField
                        label={
                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                <FontAwesomeIcon icon="hdd" style={{marginRight: '5px'}} /> Destination disk
                            </div>
                        }
                        id="destination-media"
                        medias={medias || []}
                        path={get(destinationMedia, 'path') || ''}
                        onChange={(media) => setDestinationMedia(media)}
                    />
                </Grid>
            </Grid>
            <Grid container spacing="2" direction="row" alignItems="center" sx={{mt: 2}}>
                <Grid item xs={2} lg={2}>
                    <TextField
                        label="Write retries"
                        id="retries"
                        type="number"
                        value={retries}
                        inputProps={{min: 0, style: { textAlign: 'right' }}}
                        onChange={(event) => setRetries(event.target.value)}
                    />
                </Grid>
                <Grid item xs={12}>
                    <CheckboxField
                        id="force"
                        label="Force write and ignore errors"
                        value={force}
                        onChange={(checked) => setForce(checked)}
                    />
                </Grid>
                <Grid item xs={12}>
                    <CheckboxField
                        id="verify"
                        label="Verify while writing"
                        value={verify}
                        onChange={(checked) => setVerify(checked)}
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
                                disabled={writeDisabled}
                                icon="download"
                                onClick={async () => setConfirmOpen(true)}
                            >
                                Start write
                            </Button>
                        </Stack>
                    </Box>
                </Grid>
            </Grid>
            {get(sourceMedia, 'diskInfo') && (
                <Grid container spacing="2" direction="row" alignItems="center" sx={{mt: 2}}>
                    <Grid item xs={12}>
                        <Typography variant="h3">
                            Source file
                        </Typography>
                        <Typography>
                            Disk information read from source file.
                        </Typography>                        
                        <Media media={sourceMedia}/>
                    </Grid>
                </Grid>
            )}
        </Box>
    )
}