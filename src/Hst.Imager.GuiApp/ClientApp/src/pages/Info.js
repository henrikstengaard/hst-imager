import React from 'react'
import Title from "../components/Title";
import Grid from "@mui/material/Grid";
import Box from "@mui/material/Box";
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import TextField from "../components/TextField";
import {get, isNil, set} from "lodash";
import BrowseOpenDialog from "../components/BrowseOpenDialog";
import Media from "../components/Media";
import Stack from "@mui/material/Stack";
import Radio from '@mui/material/Radio';
import RadioGroup from '@mui/material/RadioGroup';
import FormControlLabel from '@mui/material/FormControlLabel';
import FormControl from '@mui/material/FormControl';
import FormLabel from '@mui/material/FormLabel';
import RedirectButton from "../components/RedirectButton";
import Button from "../components/Button";
import MediaSelectField from "../components/MediaSelectField";
import {HubConnectionBuilder} from "@microsoft/signalr";
import {Api} from "../utils/Api";
import Typography from "@mui/material/Typography";

const initialState = {
    loading: true,
    sourceType: 'ImageFile'
}

export default function Info() {
    const [loadMedias, setLoadMedias] = React.useState(false);
    const [initialized, setInitialized] = React.useState(false);
    const [media, setMedia] = React.useState(null)
    const [medias, setMedias] = React.useState([])
    const [path, setPath] = React.useState(null)
    const [state, setState] = React.useState({ ...initialState })
    const [connection, setConnection] = React.useState(null);

    const {
        loading,
        sourceType
    } = state

    const api = React.useMemo(() => new Api(), []);

    const getMedias = React.useCallback(async () => {
        async function getMedias() {
            await api.list()
        }
        await getMedias()
    }, [api])
    
    const getPath = React.useCallback(({medias, path}) => {
        if (medias === null || medias.length === 0) {
            return null
        }

        if (path === null || path === undefined) {
            return medias[0].path
        }

        const media = medias.find(x => x.path === path)
        return media === null ? medias[0].path : media.path
    }, [])

    const getInfo = React.useCallback(async (path) => {
        const response = await fetch('api/info', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                sourceType,
                path
            })
        });
        if (!response.ok) {
            console.error('Failed to get info')
        }
    }, [sourceType])

    // initialize
    React.useEffect(() => {
        if (!loadMedias || initialized) {
            return
        }
        setInitialized(true)
        getMedias()
    }, [getMedias, initialized, loadMedias, setInitialized])

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

                    newConnection.on('List', async (medias) => {
                        const newPath = getPath({medias: medias, path: path})
                        const newMedia = newPath ? medias.find(x => x.path === newPath) : null

                        setMedias(medias || [])
                        if (newMedia) {
                            setPath(newPath)
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
    }, [connection, getInfo, getPath, path, setMedia, setMedias, setPath, sourceType])
    
    const getInfoDisabled = isNil(path)

    const handleChange = async ({name, value}) => {
        if (name === 'sourceType') {
            if (value === 'PhysicalDisk' && (medias === null || medias.length === 0)) {
                await getMedias()
            }
            const newPath = value === 'PhysicalDisk' && medias.length > 0 ? medias[0].path : null
            const newMedia = value === 'PhysicalDisk' && newPath !== null ? medias.find(x => x.path === newPath) : null
            setPath(newPath)
            setMedia(newMedia)
            if (newMedia) {
                await getInfo(newPath)
            }
        }
        set(state, name, value)
        setState({...state})
    }
    
    const handleCancel = () => {
        if (connection) {
            connection.stop()
        }
        setLoadMedias(false)
        setInitialized(false)
        setMedia(null)
        setMedias([])
        setPath(null)
        setState({ ...initialState })
        setConnection(null)
    }
    
    const handleUpdate = async () => {
        await api.list()
    }
    
    return (
        <React.Fragment>
            <Title
                text="Info"
                description="Display information about physical disk or image file."
            />
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12} lg={6}>
                    <FormControl>
                        <FormLabel id="source-type-label">Source</FormLabel>
                        <RadioGroup
                            row
                            aria-labelledby="source-type-label"
                            name="source-type"
                            value={sourceType || ''}
                            onChange={async (event) => await handleChange({
                                name: 'sourceType',
                                value: get(event, 'target.value')
                            })}
                        >
                            <FormControlLabel value="ImageFile" control={<Radio />} label="Image file" />
                            <FormControlLabel value="PhysicalDisk" control={<Radio />} label="Physical disk" />
                        </RadioGroup>
                    </FormControl>
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12} lg={6}>
                    {sourceType === 'ImageFile' && (
                        <TextField
                            id="image-path"
                            label={
                                <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                    <FontAwesomeIcon icon="file" style={{marginRight: '5px'}} /> Image file
                                </div>
                            }
                            value={path || ''}
                            endAdornment={
                                <BrowseOpenDialog
                                    id="browse-image-path"
                                    title="Select image file"
                                    onChange={async (path) => {
                                        setPath(path)
                                        await getInfo(path)
                                    }}
                                />
                            }
                            onChange={(event) => {
                                setPath(get(event, 'target.value'))
                                if (media) {
                                    setMedia(null)
                                }
                            }}
                            onKeyDown={async (event) => {
                                if (event.key !== 'Enter') {
                                    return
                                }
                                await getInfo(path)
                            }}
                        />
                    )}
                    {sourceType === 'PhysicalDisk' && (
                        <MediaSelectField
                            id="media-path"
                            label={
                                <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                    <FontAwesomeIcon icon="hdd" style={{marginRight: '5px'}} /> Physical disk
                                </div>
                            }
                            loading={loading}
                            medias={medias || []}
                            path={sourceType === 'PhysicalDisk' ? path || '' : null}
                            onChange={async (media) => {
                                setPath(media.path)
                                await getInfo(media.path)
                            }}
                        />
                    )}
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
                                icon="sync-alt"
                                onClick={async () => handleUpdate()}
                            >
                                Update
                            </Button>
                            <Button
                                disabled={getInfoDisabled}
                                icon="info"
                                onClick={async () => await getInfo(path)}
                            >
                                Get info
                            </Button>
                        </Stack>
                    </Box>
                </Grid>
            </Grid>
            {media && media.diskInfo && (
                <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                    <Grid item xs={12}>
                        <Typography variant="h3">
                            Source {(sourceType === 'ImageFile' ? 'file' : 'disk')}
                        </Typography>
                        <Typography>
                            Disk information read from source {(sourceType === 'ImageFile' ? 'file' : 'disk')}.
                        </Typography>
                        <Media media={media}/>
                    </Grid>
                </Grid>
            )}
        </React.Fragment>
    )
}