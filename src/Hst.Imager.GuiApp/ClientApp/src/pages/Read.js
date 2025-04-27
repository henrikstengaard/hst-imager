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
import {HubConnectionBuilder} from "@microsoft/signalr";
import Typography from "@mui/material/Typography";
import CheckboxField from "../components/CheckboxField";
import SelectField from "../components/SelectField";
import {BackendApiStateContext} from "../components/BackendApiContext";
import BrowseOpenDialog from "../components/BrowseOpenDialog";
import {getPartPathOptions} from "../utils/MediaHelper";
import IconButton from "@mui/material/IconButton";
import Accordion from "../components/Accordion";

const unitOptions = [{
    title: 'GB',
    value: 'gb',
    size: Math.pow(10, 9)
},{
    title: 'MB',
    value: 'mb',
    size: Math.pow(10, 6)
},{
    title: 'KB',
    value: 'kb',
    size: Math.pow(10, 3)
},{
    title: 'Bytes',
    value: 'bytes',
    size: 1
}]

const typeOptions = [{
    title: 'Image file',
    value: 'ImageFile'
}, {
    title: 'Physical disk',
    value: 'PhysicalDisk'
}]

export default function Read() {
    const [openConfirm, setOpenConfirm] = React.useState(false);
    const [sourceType, setSourceType] = React.useState('ImageFile')
    const [sourcePath, setSourcePath] = React.useState(null)
    const [sourceMedia, setSourceMedia] = React.useState(null)
    const [medias, setMedias] = React.useState(null)
    const [byteswap, setByteswap] = React.useState(false);
    const [size, setSize] = React.useState(0)
    const [unit, setUnit] = React.useState('bytes')
    const [destinationPath, setDestinationPath] = React.useState(null)
    const [startOffset, setStartOffset] = React.useState(0);
    const [readPartPath, setReadPartPath] = React.useState(null)
    const [readPartPathOptions, setReadPartPathOptions] = React.useState([])
    const [connection, setConnection] = React.useState(null);
    const {
        backendBaseUrl,
        backendApi
    } = React.useContext(BackendApiStateContext)

    const sourceTypeFormatted = sourceType === 'PhysicalDisk' ? 'physical disk' : 'image file';

    const readPartPathOption = readPartPathOptions.find(x => x.value === readPartPath)
    const formattedReadPartPath = readPartPathOption ? (readPartPath === 'custom'
        ? ` - Start offset ${startOffset}`
        : ` - ${readPartPathOption.title}`) : '';

    const unitOption = unitOptions.find(x => x.value === unit)
    const formattedSize = unitOption ? (readPartPath === 'custom'
        ? ` with size ${size} ${unitOption.title}`
        : '') : '';

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

    const getMedias = React.useCallback(async () => {
        async function getMedias() {
            await backendApi.updateList()
        }
        await getMedias()
    }, [backendApi])

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

        newConnection.on("Info", (media) => {
            setSourceMedia(media)

            // default start offset, size and unit set to largest readable size
            setStartOffset(0)
            setSize(0)
            setUnit('bytes')

            // no media, reset
            if (isNil(media)) {
                setReadPartPath(null)
                setReadPartPathOptions([])
                return
            }

            const newPartPathOptions = getPartPathOptions({media});
            
            setReadPartPath(newPartPathOptions.length > 0 ? newPartPathOptions[0].value : null)
            setReadPartPathOptions(newPartPathOptions)
        });

        newConnection.on('List', async (medias) => {
            setMedias(medias || [])

            const newMedia = getMedia({medias: medias, path: get(sourceMedia, 'path')});
            if (!newMedia) {
                return;
            }

            const newPath = get(newMedia, 'path');
            setSourcePath(newPath)
            await getInfo(newPath, byteswap)
        })

        newConnection.start();

        setConnection(newConnection);

        return () => {
            if (!connection) {
                return
            }
            
            connection.stop();
        };
    }, [backendApi, backendBaseUrl, byteswap, connection, getInfo, getMedia, getMedias, setConnection, sourceMedia])
    
    const handleRead = async () => {
        await backendApi.startRead({
            title: `Reading source ${sourceTypeFormatted} '${sourceMedia.name}${formattedReadPartPath}' to destination image file '${destinationPath}'${formattedSize}`,
            sourcePath: isNil(readPartPath) || readPartPath === 'custom' ? sourceMedia.path : readPartPath,
            destinationPath,
            startOffset,
            size: (size * unitOption.size),
            byteswap
        });
    }

    const handleConfirm = async (confirmed) => {
        setOpenConfirm(false)
        if (!confirmed) {
            return
        }
        await handleRead()
    }

    const handleUpdate = async () => {
        await backendApi.updateList()
    }

    const handleCancel = () => {
        if (connection) {
            connection.stop()
        }
        setOpenConfirm(false)
        setSourceType('ImageFile')
        setSourcePath(null)
        setSourceMedia(null)
        setMedias(null)
        setByteswap(false)
        setSize(0)
        setUnit('bytes')
        setDestinationPath(null)
        setStartOffset(0);
        setReadPartPath(null)
        setReadPartPathOptions([])
        setConnection(null)
    }
    
    const readDisabled = isNil(sourceMedia) || isNil(destinationPath) || destinationPath === ''

    return (
        <Box>
            <ConfirmDialog
                id="confirm-read"
                open={openConfirm}
                title="Read"
                description={`Do you want to read source ${sourceTypeFormatted} '${sourceMedia === null ? '' : sourceMedia.name}${formattedReadPartPath}' to destination image file '${destinationPath}'${formattedSize}?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />
            <Title
                text="Read"
                description="Read an image file, a physical disk or part of to an image file."
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
                            setSourcePath(null);
                            setSourceMedia(null);
                            setReadPartPath(null);
                            setReadPartPathOptions([]);
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
                            id="source-image-path"
                            label={
                                <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                    <FontAwesomeIcon icon="file" style={{marginRight: '5px'}} /> Source image file
                                </div>
                            }
                            value={sourcePath || ''}
                            endAdornment={
                                <BrowseOpenDialog
                                    id="browse-source-image-path"
                                    title="Select source image file"
                                    onChange={async (path) => {
                                        setSourcePath(path)
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
                                setSourcePath(get(event, 'target.value'))
                                if (sourceMedia) {
                                    setSourceMedia(null)
                                }
                            }}
                            onKeyDown={async (event) => {
                                if (event.key !== 'Enter') {
                                    return
                                }
                                console.log("get info")
                                await getInfo(sourcePath, byteswap)
                            }}
                        />
                    </Grid>
                )}
                {sourceType === 'PhysicalDisk' && (
                    <Grid item xs={12} lg={6}>
                        <Stack direction="row">
                            <MediaSelectField
                                label={
                                    <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                        <FontAwesomeIcon icon="hdd" style={{marginRight: '5px'}} /> Source physical disk
                                    </div>
                                }
                                id="source-disk"
                                medias={medias || []}
                                path={get(sourceMedia, 'path') || ''}
                                onChange={async (media) => {
                                    setSourceMedia(media)
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
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
                <Grid item xs={12} lg={6}>
                    <TextField
                        id="destination-file"
                        label={
                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                <FontAwesomeIcon icon="file" style={{marginRight: '5px'}} /> Destination image file
                            </div>
                        }
                        value={destinationPath || ''}
                        endAdornment={
                            <BrowseSaveDialog
                                id="read-destination-path"
                                title="Select destination image"
                                onChange={(path) => setDestinationPath(path)}
                                fileFilters = {[{
                                    name: 'Hard disk image files',
                                    extensions: ['img', 'hdf', 'vhd', 'gz', 'zip']
                                }, {
                                    name: 'All files',
                                    extensions: ['*']
                                }]}
                            />
                        }
                        onChange={(event) => setDestinationPath(get(event, 'target.value'))}
                        onKeyDown={async (event) => {
                            if (event.key !== 'Enter') {
                                return
                            }
                            setOpenConfirm(true)
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={0} direction="row" alignItems="center" sx={{ mt: 0 }}>
                <Grid item xs={12} lg={6}>
                    <Accordion title="Advanced" icon="gear" expanded={false} border={false}>
                        <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
                            <Grid item xs={12} lg={6}>
                                <SelectField
                                    label={
                                        <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                            <FontAwesomeIcon icon="file-fragment" style={{marginRight: '5px'}} /> {`Part of source ${sourceType === 'PhysicalDisk' ? 'physical disk' : 'image file'} to read`}
                                        </div>
                                    }
                                    id="read-part-path"
                                    disabled={isNil(sourceMedia)}
                                    emptyLabel="None available"
                                    value={readPartPath || ''}
                                    options={readPartPathOptions || []}
                                    onChange={(value) => {
                                        setReadPartPath(value)
                                        setStartOffset(0);
                                        setSize(value === 'custom' ? get(sourceMedia, 'diskSize') || 0 : 0)
                                        setUnit('bytes')
                                    }}
                                />
                            </Grid>
                        </Grid>
                        <Grid container spacing={1} direction="row" sx={{mt: 0.3}}>
                            <Grid item xs={12} lg={6}>
                                <TextField
                                    label={
                                        <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                            <FontAwesomeIcon icon="location-crosshairs" style={{marginRight: '5px'}} /> Start offset
                                        </div>
                                    }
                                    id="start-offset"
                                    disabled={readPartPath !== 'custom'}
                                    type={"number"}
                                    value={startOffset}
                                    inputProps={{min: 0, style: { textAlign: 'right' }}}
                                    onChange={(event) => setStartOffset(event.target.value)}
                                />
                            </Grid>
                        </Grid>
                        <Grid container spacing={1} direction="row" sx={{mt: 0.3}}>
                            <Grid item xs={8} lg={4}>
                                <TextField
                                    label={
                                        <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                            <FontAwesomeIcon icon="ruler-horizontal" style={{marginRight: '5px'}} /> Size
                                        </div>
                                    }
                                    id="size"
                                    disabled={readPartPath !== 'custom'}
                                    type={"number"}
                                    value={size}
                                    inputProps={{min: 0, style: { textAlign: 'right' }}}
                                    onChange={(event) => setSize(event.target.value)}
                                />
                            </Grid>
                            <Grid item xs={4} lg={2}>
                                <SelectField
                                    label={
                                        <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                            <FontAwesomeIcon icon="scale-balanced" style={{marginRight: '5px'}} /> Unit
                                        </div>
                                    }
                                    id="unit"
                                    disabled={readPartPath !== 'custom'}
                                    value={unit || ''}
                                    options={unitOptions}
                                    onChange={(value) => setUnit(value)}
                                />
                            </Grid>
                        </Grid>
                        <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0}}>
                            <Grid item xs={12}>
                                <CheckboxField
                                    id="byteswap"
                                    label="Byteswap source sectors"
                                    value={byteswap}
                                    onChange={async (checked) => {
                                        setByteswap(checked)
                                        if (sourceMedia) {
                                            await getInfo(sourceMedia.path, checked)
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
                                disabled={readDisabled}
                                icon="upload"
                                onClick={async () => setOpenConfirm(true)}
                            >
                                Start read
                            </Button>
                        </Stack>
                    </Box>
                </Grid>
            </Grid>
            {get(sourceMedia, 'diskInfo') && (
                <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                    <Grid item xs={12}>
                        <Typography variant="h3">
                            Source {sourceTypeFormatted}
                        </Typography>
                        <Typography>
                            Disk information read from source {sourceTypeFormatted}.
                        </Typography>
                        <Media media={sourceMedia}/>
                    </Grid>
                </Grid>
            )}
        </Box>
    )
}