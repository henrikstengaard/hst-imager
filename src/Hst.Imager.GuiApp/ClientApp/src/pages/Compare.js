import React from 'react'
import Box from "@mui/material/Box";
import Title from "../components/Title";
import Grid from "@mui/material/Grid";
import FormControl from "@mui/material/FormControl";
import FormLabel from "@mui/material/FormLabel";
import RadioGroup from "@mui/material/RadioGroup";
import FormControlLabel from "@mui/material/FormControlLabel";
import Radio from "@mui/material/Radio";
import TextField from "../components/TextField";
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import BrowseOpenDialog from "../components/BrowseOpenDialog";
import {get, isNil, set} from "lodash";
import MediaSelectField from "../components/MediaSelectField";
import Stack from "@mui/material/Stack";
import RedirectButton from "../components/RedirectButton";
import Button from "../components/Button";
import ConfirmDialog from "../components/ConfirmDialog";
import {Api} from "../utils/Api";
import {HubConnectionBuilder} from "@microsoft/signalr";
import Typography from "@mui/material/Typography";
import Media from "../components/Media";
import CheckboxField from "../components/CheckboxField";

export default function Verify() {
    const [confirmOpen, setConfirmOpen] = React.useState(false);
    const [sourceMedia, setSourceMedia] = React.useState(null)
    const [sourcePath, setSourcePath] = React.useState(null)
    const [medias, setMedias] = React.useState(null)
    const [destinationPath, setDestinationPath] = React.useState(null)
    const [sourceType, setSourceType] = React.useState('ImageFile')
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
                sourceType: sourceType,
                path
            })
        });
        if (!response.ok) {
            console.error('Failed to get info')
        }
    }

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
                        const newPath = getPath({medias: medias, path: sourcePath})
                        const newMedia = newPath ? medias.find(x => x.path === newPath) : null

                        setMedias(medias || [])
                        if (newMedia) {
                            setSourcePath(newPath)
                            setSourceMedia(newMedia)
                            await getInfo(newPath)
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
    }, [connection, getInfo, getPath, setMedias, setSourcePath, setSourceMedia, sourcePath])

    const handleSourceTypeChange = async (value) => {
        setSourceType(value)
        switch (value) {
            case 'ImageFile':
                setSourcePath(null)
                setSourceMedia(null)
                break
            case 'PhysicalDisk':
                if (medias === null) {
                    setSourcePath(null)
                    setSourceMedia(null)
                    await handleGetMedias()
                    return
                }

                const newPath = getPath({medias: medias, path: null})
                const newMedia = newPath ? medias.find(x => x.path === newPath) : null

                setSourcePath(newPath)
                setSourceMedia(newMedia)
                if (value === 'PhysicalDisk' && newMedia) {
                    await getInfo(newPath)
                }

                break
        }
    }
    
    const handleCompare = async () => {
        const response = await fetch('api/compare', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                title: `Comparing ${(sourceType === 'ImageFile' ? 'file' : 'disk')} '${isNil(sourceMedia) ? sourcePath : sourceMedia.name}' and file '${destinationPath}'`,
                sourceType,
                sourcePath,
                destinationPath,
                retries,
                force
            })
        });
        if (!response.ok) {
            console.error('Failed to compare')
        }
    }

    const handleConfirm = async (confirmed) => {
        setConfirmOpen(false)
        if (!confirmed) {
            return
        }
        await handleCompare()
    }

    const handleCancel = () => {
        if (connection) {
            connection.stop()
        }
        setConfirmOpen(false)
        setSourceMedia(null)
        setSourcePath(null)
        setMedias(null)
        setDestinationPath(null)
        setSourceType('ImageFile')
        setForce(false)
        setRetries(5)
        setConnection(null)
    }
    
    const handleUpdate = async () => {
        await api.list()
    }

    const compareDisabled = isNil(sourcePath) || isNil(destinationPath)
    
    return (
        <Box>
            <ConfirmDialog
                id="confirm-compare"
                open={confirmOpen}
                title="Compare"
                description={`Do you want to compare '${isNil(sourceMedia) ? sourcePath : sourceMedia.name}' and '${destinationPath}'?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />
            <Title
                text="Compare"
                description="Compare an image file or physical disk against an image file comparing them byte by byte."
            />
            <Grid container spacing="2" direction="row" alignItems="center" sx={{mt: 2}}>
                <Grid item xs={12} lg={6}>
                    <FormControl>
                        <FormLabel id="source-type-label">Source</FormLabel>
                        <RadioGroup
                            row
                            aria-labelledby="source-type-label"
                            name="source-type"
                            value={sourceType || ''}
                            onChange={(event) => handleSourceTypeChange(event.target.value)}
                        >
                            <FormControlLabel value="ImageFile" control={<Radio />} label="Image file" />
                            <FormControlLabel value="PhysicalDisk" control={<Radio />} label="Physical disk" />
                        </RadioGroup>
                    </FormControl>
                </Grid>
            </Grid>
            <Grid container spacing="2" direction="row" alignItems="center" sx={{mt: 2}}>
                <Grid item xs={12} lg={6}>
                    {sourceType === 'ImageFile' && (
                        <TextField
                            id="source-image-path"
                            label={
                                <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                    <FontAwesomeIcon icon="file" style={{marginRight: '5px'}} /> Source file
                                </div>
                            }
                            value={sourcePath || ''}
                            endAdornment={
                                <BrowseOpenDialog
                                    id="browse-source-image-path"
                                    title="Select source file"
                                    onChange={async (path) => {
                                        setSourcePath(path)
                                        await getInfo(path)
                                    }}
                                />
                            }
                            onChange={(event) => {
                                setSourcePath(get(event, 'target.value'))
                                if (sourceMedia) {
                                    setSourceMedia(null)
                                }
                            }}
                            onKeyDown={async (event) => {
                                if (event.key !== 'Enter') {
                                    return
                                }
                                await getInfo(sourcePath)
                            }}
                        />
                    )}
                    {sourceType === 'PhysicalDisk' && (
                        <MediaSelectField
                            id="source-media-path"
                            label={
                                <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                    <FontAwesomeIcon icon="hdd" style={{marginRight: '5px'}} /> Source disk
                                </div>
                            }
                            // loading={loading}
                            medias={medias || []}
                            path={sourcePath || ''}
                            onChange={async (media) => {
                                setSourcePath(media)
                                setSourceMedia(media.path)
                                await getInfo(media.path)
                            }}                        
                        />
                    )}
                </Grid>
            </Grid>
            <Grid container spacing="2" direction="row" alignItems="center" sx={{mt: 2}}>
                <Grid item xs={12} lg={6}>
                    <TextField
                        id="destination-path"
                        label={
                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                <FontAwesomeIcon icon="file" style={{marginRight: '5px'}} /> Destination file
                            </div>
                        }
                        value={destinationPath || ''}
                        endAdornment={
                            <BrowseOpenDialog
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
            <Grid container spacing="2" direction="row" alignItems="center" sx={{mt: 2}}>
                <Grid item xs={2} lg={2}>
                    <TextField
                        label="Read retries"
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
                        label="Force read and ignore errors"
                        value={force}
                        onChange={(checked) => setForce(checked)}
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
                                disabled={compareDisabled}
                                icon="check"
                                onClick={async () => setConfirmOpen(true)}
                            >
                                Start compare
                            </Button>
                        </Stack>
                    </Box>
                </Grid>
            </Grid>
            {sourceMedia && sourceMedia.diskInfo && (
                <Grid container spacing="2" direction="row" alignItems="center" sx={{mt: 2}}>
                    <Grid item xs={12}>
                        <Typography variant="h3">
                            Source {(sourceType === 'ImageFile' ? 'file' : 'disk')}
                        </Typography>
                        <Typography>
                            Disk information read from source {(sourceType === 'ImageFile' ? 'file' : 'disk')}.
                        </Typography>
                        <Media media={sourceMedia}/>
                    </Grid>
                </Grid>
            )}
        </Box>
    )        
}