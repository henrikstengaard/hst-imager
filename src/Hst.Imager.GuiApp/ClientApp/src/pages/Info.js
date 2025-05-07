import React from 'react'
import Title from "../components/Title";
import Grid from "@mui/material/Grid";
import Box from "@mui/material/Box";
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import TextField from "../components/TextField";
import {get, isNil} from "lodash";
import BrowseOpenDialog from "../components/BrowseOpenDialog";
import Media from "../components/Media";
import Stack from "@mui/material/Stack";
import RedirectButton from "../components/RedirectButton";
import Button from "../components/Button";
import MediaSelectField from "../components/MediaSelectField";
import {HubConnectionBuilder} from "@microsoft/signalr";
import Typography from "@mui/material/Typography";
import CheckboxField from "../components/CheckboxField";
import {BackendApiStateContext} from "../components/BackendApiContext";
import IconButton from "@mui/material/IconButton";
import SelectField from "../components/SelectField";
import Accordion from "../components/Accordion";
import {formatBytes} from "../utils/Format";

const typeOptions = [{
    title: 'Image file',
    value: 'ImageFile'
}, {
    title: 'Physical disk',
    value: 'PhysicalDisk'
}]

let piStormDiskSelected = false;

export default function Info() {
    const [loading, setLoading] = React.useState(false)
    const [media, setMedia] = React.useState(null)
    const [medias, setMedias] = React.useState(null)
    const [path, setPath] = React.useState(null)
    const [sourceType, setSourceType] = React.useState('ImageFile')
    const [piStormDiskPath, setPiStormDiskPath] = React.useState(null)
    const [piStormDiskOptions, setPiStormDiskOptions] = React.useState([])
    const [piStormMedia, setPiStormMedia] = React.useState(null)
    const [byteswap, setByteswap] = React.useState(false)
    const [connection, setConnection] = React.useState(null);
    const {
        backendBaseUrl,
        backendApi
    } = React.useContext(BackendApiStateContext)
    
    const getMedias = React.useCallback(async () => {
        async function getMedias() {
            setLoading(true);
            await backendApi.updateList();
        }
        await getMedias()
    }, [backendApi])
    
    const getMedia = React.useCallback(({medias, path}) => {
        if (medias === null || medias.length === 0) {
            return null
        }

        if (path === null || path === undefined) {
            return medias[0]
        }

        const media = medias.find(x => x.path === path)
        return media === null ? medias[0] : media
    }, [])

    const getInfo = React.useCallback(async (path, byteswap) => {
        await backendApi.updateInfo({ path, byteswap });
    }, [backendApi])
    
    React.useEffect(() => {
        if (connection) {
            return
        }

        const newConnection = new HubConnectionBuilder()
            .withUrl(`${backendBaseUrl}hubs/result`)
            .withAutomaticReconnect()
            .build();

        newConnection.on("Info", (newMedia) => {
            if (piStormDiskSelected) {
                setPiStormMedia(newMedia);
                return;
            }

            setMedia(newMedia)

            const mbrPartitionTablePart = get(newMedia, 'diskInfo.mbrPartitionTablePart');
            
            if (!mbrPartitionTablePart)
            {
                setPiStormDiskPath(null);
                setPiStormDiskOptions([]);
                return;
            }

            const directoryPathSeparator = newMedia.path.startsWith('/') ? '/' : '\\'

            const newPiStormDiskOptions = [{
                title: `Disk (${formatBytes(newMedia.diskSize)})`,
                value: newMedia.path
            }]

            mbrPartitionTablePart.parts.filter(part => part.partType === 'Partition' && !isNil(part.biosType) && part.biosType === '118').forEach(part => {
                const type = part.partitionType === part.fileSystem
                    ? part.partitionType
                    : `${part.partitionType}, ${part.fileSystem}`;

                newPiStormDiskOptions.push({
                    title: `Partition #${part.partitionNumber}: ${type} (${formatBytes(part.size)})`,
                    value: newMedia.path + directoryPathSeparator + 'mbr' + directoryPathSeparator + part.partitionNumber
                })
            })

            setPiStormDiskPath(newPiStormDiskOptions.length > 0 ? newPiStormDiskOptions[0].value : null)
            setPiStormDiskOptions(newPiStormDiskOptions);
        });

        newConnection.on('List', async (medias) => {
            setLoading(false)
            setMedias(medias || [])

            const newMedia = getMedia({medias: medias, path: path});
            const newPath = get(newMedia, 'path');

            if (newMedia) {
                setPath(newPath)
                await getInfo(newPath, byteswap)
            }
        })

        newConnection.start();

        setConnection(newConnection);

        return () => {
            if (!connection) {
                return
            }

            connection.stop();
        };
    }, [backendBaseUrl, byteswap, connection, getInfo, getMedia, path, setConnection, sourceType])
    
    const getInfoDisabled = isNil(path)

    const handleCancel = () => {
        setLoading(false)
        setMedia(null)
        setMedias(null)
        setPath(null)
        setSourceType(null)
        setPiStormDiskPath(null)
        setPiStormDiskOptions([])
        setPiStormMedia(null)
        setByteswap(false)
    }
    
    const handleUpdate = async () => {
        await backendApi.updateList()
    }
    
    const currentMedia = piStormMedia || media;
    
    return (
        <React.Fragment>
            <Title
                text="Info"
                description="Display information about an image file or a physical disk."
            />
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label={
                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                <FontAwesomeIcon icon="upload" style={{marginRight: '5px'}} /> Source
                            </div>
                        }
                        id="source"
                        emptyLabel="None available"
                        value={sourceType || 'ImageFile' }
                        options={typeOptions}
                        onChange={(value) => {
                            setSourceType(value);
                            setPath(null);
                            setMedia(null);
                            setPiStormDiskPath(null)
                            setPiStormDiskOptions([])
                            setPiStormMedia(null)
                            piStormDiskSelected = false;
                            if (value === 'PhysicalDisk') {
                                getMedias();
                            }
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
                {sourceType === 'ImageFile' && (
                    <Grid item xs={12} lg={6}>
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
                                    id="info-image-path"
                                    title="Select image file"
                                    onChange={async (path) => {
                                        setPath(path)
                                        setMedia(null)
                                        setPiStormDiskPath(null)
                                        setPiStormDiskOptions([])
                                        setPiStormMedia(null)
                                        piStormDiskSelected = false;
                                        await getInfo(path, byteswap)
                                    }}
                                    fileFilters = {[{
                                        name: 'Hard disk image files',
                                        extensions: ['img', 'hdf', 'vhd', 'xz', 'gz', 'zip', 'rar']
                                    }, {
                                        name: 'All files',
                                        extensions: ['*']
                                    }]}
                                />
                            }
                            onChange={(event) => {
                                setPath(get(event, 'target.value'))
                                if (media) {
                                    setMedia(null)
                                    setPiStormDiskPath(null)
                                    setPiStormDiskOptions([])
                                    setPiStormMedia(null)
                                    piStormDiskSelected = false;
                                }
                            }}
                            onKeyDown={async (event) => {
                                if (event.key !== 'Enter') {
                                    return
                                }
                                await getInfo(path, byteswap)
                            }}
                        />
                    </Grid>
                )}
                {sourceType === 'PhysicalDisk' && (
                    <Grid item xs={12} lg={6}>
                        <Stack direction="row">
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
                                    setMedia(null)
                                    setPiStormDiskPath(null)
                                    setPiStormDiskOptions([])
                                    setPiStormMedia(null)
                                    piStormDiskSelected = false;
                                    await getInfo(media.path, byteswap)
                                }}
                            />
                            <IconButton
                                aria-label="refresh"
                                color="primary"
                                disableFocusRipple={true}
                                onClick={async () => handleUpdate()}
                            >
                                <FontAwesomeIcon icon="sync-alt" />
                            </IconButton>
                        </Stack>
                    </Grid>
                )}
            </Grid>
            {piStormDiskOptions.length > 1 && (
                <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
                    <Grid item xs={12} lg={6}>
                        <SelectField
                            label={
                                <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                    <FontAwesomeIcon icon="file-fragment" style={{marginRight: '5px'}} /> PiStorm disk to read
                                </div>
                            }
                            id="pistorm-disk-path"
                            emptyLabel="None available"
                            value={piStormDiskPath || '' }
                            options={piStormDiskOptions}
                            onChange={async (value) => {
                                piStormDiskSelected = true;
                                setPiStormDiskPath(value);
                                await getInfo(value, byteswap)
                            }}
                        />
                    </Grid>
                </Grid>
            )}
            <Grid container spacing={0} direction="row" alignItems="center" sx={{ mt: 0 }}>
                <Grid item xs={12} lg={6}>
                    <Accordion title="Advanced" icon="gear" expanded={false} border={false}>
                        <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
                            <Grid item xs={12} lg={6}>
                                <CheckboxField
                                    id="byteswap"
                                    label="Byteswap sectors"
                                    value={byteswap}
                                    onChange={async (checked) => {
                                        setByteswap(checked)
                                        if (media) {
                                            await getInfo(path, checked)
                                        }
                                    }}
                                />
                            </Grid>
                        </Grid>
                    </Accordion>
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0}}>
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
                                disabled={getInfoDisabled}
                                icon="info"
                                onClick={async () => await getInfo(path, byteswap)}
                            >
                                Get info
                            </Button>
                        </Stack>
                    </Box>
                </Grid>
            </Grid>
            {currentMedia && currentMedia.diskInfo && (
                <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                    <Grid item xs={12}>
                        <Typography variant="h3">
                            Source {(sourceType === 'ImageFile' ? 'image file' : 'physical disk')}
                        </Typography>
                        <Typography>
                            Disk information read from source {(sourceType === 'ImageFile' ? 'image file' : 'physical disk')}.
                        </Typography>
                        <Media media={currentMedia}/>
                    </Grid>
                </Grid>
            )}
        </React.Fragment>
    )
}