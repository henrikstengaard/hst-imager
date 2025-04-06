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
import {HubConnectionBuilder} from "@microsoft/signalr";
import Media from "../components/Media";
import Typography from "@mui/material/Typography";
import CheckboxField from "../components/CheckboxField";
import SelectField from "../components/SelectField";
import {BackendApiStateContext} from "../components/BackendApiContext";
import FormControl from "@mui/material/FormControl";
import FormLabel from "@mui/material/FormLabel";
import RadioGroup from "@mui/material/RadioGroup";
import FormControlLabel from "@mui/material/FormControlLabel";
import Radio from "@mui/material/Radio";
import {getPartPathOptions} from "../utils/MediaHelper";

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

let updateTarget = '';

export default function Write() {
    const [openConfirm, setOpenConfirm] = React.useState(false);
    const [sourcePath, setSourcePath] = React.useState(null)
    const [sourceMedia, setSourceMedia] = React.useState(null)
    const [destinationPath, setDestinationPath] = React.useState(null)
    const [destinationMedia, setDestinationMedia] = React.useState(null)
    const [destinationType, setDestinationType] = React.useState('ImageFile')
    const [medias, setMedias] = React.useState(null)
    const [byteswap, setByteswap] = React.useState(false);
    const [size, setSize] = React.useState(0)
    const [unit, setUnit] = React.useState('bytes')
    const [startOffset, setStartOffset] = React.useState(0);
    const [writePartPath, setWritePartPath] = React.useState(null)
    const [writePartPathOptions, setWritePartPathOptions] = React.useState([])
    const [connection, setConnection] = React.useState(null);
    const {
        backendBaseUrl,
        backendApi
    } = React.useContext(BackendApiStateContext)

    const writePartPathOption = writePartPathOptions.find(x => x.value === writePartPath)
    const formattedWritePartPath = writePartPathOption ? (writePartPath === 'custom'
        ? ` - Start offset ${startOffset}`
        : ` - ${writePartPathOption.title}`) : '';

    const unitOption = unitOptions.find(x => x.value === unit)
    const formattedSize = unitOption ? (writePartPath === 'custom'
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

    const getInfo = React.useCallback(async (target, path, type, byteswap) => {
        updateTarget = target;
        await backendApi.updateInfo({ path, sourceType: type, byteswap });
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
            if (updateTarget === 'Source') {
                setSourceMedia(media);
                return;
            }

            if (updateTarget !== 'Destination') {
                return;
            }

            setDestinationMedia(media)

            // default start offset, size and unit set to largest writable size
            setStartOffset(0)
            setSize(0)
            setUnit('bytes')

            // no media, reset
            if (isNil(media)) {
                setWritePartPath(null)
                setWritePartPathOptions([])
                return
            }

            const newPartPathOptions = getPartPathOptions(media);

            setWritePartPath(newPartPathOptions.length > 0 ? newPartPathOptions[0].value : null)
            setWritePartPathOptions(newPartPathOptions)
        });

        newConnection.on('List', async (medias) => {
            setMedias(medias || [])

            const newMedia = getMedia({medias: medias, path: get(destinationMedia, 'path')});
            if (!newMedia) {
                return;
            }

            const newPath = get(newMedia, 'path');
            setDestinationPath(newPath)
            await getInfo('Destination', newPath, 'PhysicalDisk', byteswap)
        })

        newConnection.start();

        setConnection(newConnection);

        return () => {
            if (!connection) {
                return
            }

            connection.stop();
        };
    }, [backendBaseUrl, byteswap, connection, destinationMedia, destinationPath, getInfo, getMedia, getMedias, setConnection, sourcePath])
    
    const handleWrite = async () => {
        await backendApi.startWrite({
            title: `Writing file '${sourcePath}' to disk '${get(destinationMedia, 'name') || ''}${formattedWritePartPath}'${formattedSize}`,
            writePhysicalDisk: destinationType === 'PhysicalDisk',
            sourcePath,
            destinationPath: isNil(writePartPath) || writePartPath === 'custom' ? destinationMedia.path : writePartPath,
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
        await handleWrite()
    }

    const handleUpdate = async () => {
        await backendApi.updateList()
    }

    const handleCancel = () => {
        if (connection) {
            connection.stop()
        }
        setOpenConfirm(false)
        setSourcePath(null)
        setSourceMedia(null)
        setDestinationPath(null)
        setDestinationMedia(null)
        setMedias([])
        setByteswap(false)
        setSize(0)
        setUnit('bytes')
        setWritePartPath(null)
        setWritePartPathOptions([])
        setConnection(null)
    }
    
    const writeDisabled = isNil(sourceMedia) || isNil(destinationMedia)

    return (
        <Box>
            <ConfirmDialog
                id="confirm-write"
                open={openConfirm}
                title="Write"
                description={`Do you want to write file '${sourcePath}' to disk '${get(destinationMedia, 'name') || ''}${formattedWritePartPath}'${formattedSize}?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />
            <Title
                text="Write"
                description="Write image file or part of (partition or custom) to a disk."
            />
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
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
                                    await getInfo('Source', path, 'ImageFile', byteswap)
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
                            await getInfo('Source', sourcePath, 'ImageFile', byteswap)
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12} lg={6}>
                    <FormControl>
                        <FormLabel id="destination-type-label">Destination</FormLabel>
                        <RadioGroup
                            row
                            aria-labelledby="destination-type-label"
                            name="destination-type"
                            value={destinationType || ''}
                            onChange={async (event) => {
                                updateTarget = 'Destination';
                                const value = get(event, 'target.value');
                                setDestinationType(value);
                                setDestinationPath(null);
                                setDestinationMedia(null);
                                setWritePartPath(null);
                                setWritePartPathOptions([]);
                                if (value === 'PhysicalDisk') {
                                    getMedias();
                                }
                            }}
                        >
                            <FormControlLabel value="ImageFile" control={<Radio />} label="Image file" />
                            <FormControlLabel value="PhysicalDisk" control={<Radio />} label="Physical disk" />
                        </RadioGroup>
                    </FormControl>
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12} lg={6}>
                    {destinationType === 'ImageFile' && (
                        <TextField
                            id="destination-image-path"
                            label={
                                <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                    <FontAwesomeIcon icon="file" style={{marginRight: '5px'}} /> Destination image file
                                </div>
                            }
                            value={destinationPath || ''}
                            endAdornment={
                                <BrowseOpenDialog
                                    id="browse-destination-image-path"
                                    title="Select destination image file"
                                    onChange={async (path) => {
                                        setDestinationPath(path)
                                        await getInfo('Destination', path, 'ImageFile', byteswap)
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
                                setDestinationPath(get(event, 'target.value'))
                                if (destinationMedia) {
                                    setDestinationMedia(null)
                                }
                            }}
                            onKeyDown={async (event) => {
                                if (event.key !== 'Enter') {
                                    return
                                }
                                await getInfo('Destination', destinationPath, 'ImageFile', byteswap)
                            }}
                        />
                    )}
                    {destinationType === 'PhysicalDisk' && (
                        <MediaSelectField
                            label={
                                <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                    <FontAwesomeIcon icon="hdd" style={{marginRight: '5px'}} /> Destination disk
                                </div>
                            }
                            id="destination-disk"
                            medias={medias || []}
                            path={get(destinationMedia, 'path') || ''}
                            onChange={(media) => setDestinationMedia(media)}
                        />
                    )}
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0}}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label={`Part of destination ${destinationType === 'PhysicalDisk' ? 'physical disk' : 'image file'} to write to`}
                        id="write-part-path"
                        emptyLabel="None available"
                        value={writePartPath || ''}
                        options={writePartPathOptions || []}
                        onChange={(value) => {
                            setWritePartPath(value)
                            setStartOffset(0);
                            setSize(value === 'custom' ? get(sourceMedia, 'diskSize') || 0 : 0)
                            setUnit('bytes')
                        }}
                    />
                </Grid>
            </Grid>
            {writePartPath === 'custom' && (
                <React.Fragment>
                    <Grid container spacing={1} direction="row" sx={{mt: 0}}>
                        <Grid item xs={12} lg={6}>
                            <TextField
                                label="Start offset"
                                id="start-offset"
                                type={"number"}
                                value={startOffset}
                                inputProps={{min: 0, style: { textAlign: 'right' }}}
                                onChange={(event) => setStartOffset(event.target.value)}
                            />
                        </Grid>
                    </Grid>
                    <Grid container spacing={1} direction="row" sx={{mt: 0}}>
                        <Grid item xs={8} lg={4}>
                            <TextField
                                label="Size"
                                id="size"
                                type={"number"}
                                value={size}
                                inputProps={{min: 0, style: { textAlign: 'right' }}}
                                onChange={(event) => setSize(event.target.value)}
                            />
                        </Grid>
                        <Grid item xs={4} lg={2}>
                            <SelectField
                                label="Unit"
                                id="unit"
                                value={unit || ''}
                                options={unitOptions}
                                onChange={(value) => setUnit(value)}
                            />
                        </Grid>
                    </Grid>
                </React.Fragment>
            )}
            <Grid container spacing={0} direction="row" alignItems="center" sx={{mt: 0}}>
                <Grid item xs={12}>
                    <CheckboxField
                        id="byteswap"
                        label="Byteswap source sectors"
                        value={byteswap}
                        onChange={async (checked) => {
                            setByteswap(checked)
                            if (sourceMedia) {
                                await getInfo('Source', sourceMedia.path, 'ImageFile', checked)
                            }
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={0} direction="row" alignItems="center" sx={{mt: 0}}>
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
                                disabled={writeDisabled}
                                icon="download"
                                onClick={async () => setOpenConfirm(true)}
                            >
                                Start write
                            </Button>
                        </Stack>
                    </Box>
                </Grid>
            </Grid>
            {get(sourceMedia, 'diskInfo') && (
                <Grid container spacing={0} direction="row" alignItems="center" sx={{mt: 0}}>
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