import React from 'react'
import Box from "@mui/material/Box";
import Title from "../components/Title";
import Grid from "@mui/material/Grid";
import TextField from "../components/TextField";
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import BrowseOpenDialog from "../components/BrowseOpenDialog";
import {get, isNil} from "lodash";
import MediaSelectField from "../components/MediaSelectField";
import Stack from "@mui/material/Stack";
import RedirectButton from "../components/RedirectButton";
import Button from "../components/Button";
import ConfirmDialog from "../components/ConfirmDialog";
import {HubConnectionBuilder} from "@microsoft/signalr";
import Typography from "@mui/material/Typography";
import Media from "../components/Media";
import CheckboxField from "../components/CheckboxField";
import SelectField from "../components/SelectField";
import {BackendApiStateContext} from "../components/BackendApiContext";
import {getPartPathOptions} from "../utils/MediaHelper";
import Accordion from "../components/Accordion";
import IconButton from "@mui/material/IconButton";

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

let updateTarget = '';

export default function Verify() {
    const [openConfirm, setOpenConfirm] = React.useState(false);
    const [sourceType, setSourceType] = React.useState('ImageFile')
    const [sourcePath, setSourcePath] = React.useState(null)
    const [sourceMedia, setSourceMedia] = React.useState(null)
    const [srcStartOffset, setSrcStartOffset] = React.useState(0);
    const [srcPartPath, setSrcPartPath] = React.useState(null)
    const [srcPartPathOptions, setSrcPartPathOptions] = React.useState([])
    const [byteswap, setByteswap] = React.useState(false);
    const [medias, setMedias] = React.useState(null)
    const [size, setSize] = React.useState(0)
    const [unit, setUnit] = React.useState('bytes')
    const [destinationType, setDestinationType] = React.useState('ImageFile')
    const [destinationPath, setDestinationPath] = React.useState(null)
    const [destinationMedia, setDestinationMedia] = React.useState(null)
    const [destStartOffset, setDestStartOffset] = React.useState(0);
    const [destPartPath, setDestPartPath] = React.useState(null)
    const [destPartPathOptions, setDestPartPathOptions] = React.useState([])
    const [connection, setConnection] = React.useState(null);
    const {
        backendBaseUrl,
        backendApi
    } = React.useContext(BackendApiStateContext)

    const sourceTypeFormatted = sourceType === 'PhysicalDisk' ? 'physical disk' : 'image file';
    const destinationTypeFormatted = destinationType === 'PhysicalDisk' ? 'physical disk' : 'image file';

    const srcPartPathOption = srcPartPathOptions.find(x => x.value === srcPartPath)
    const formattedSrcPartPath = srcPartPathOption ? (srcPartPath === 'custom'
        ? ` - Start offset ${srcStartOffset}`
        : ` - ${srcPartPathOption.title}`) : '';

    const destPartPathOption = destPartPathOptions.find(x => x.value === destPartPath)
    const formattedDestPartPath = destPartPathOption ? (destPartPath === 'custom'
        ? ` - Start offset ${destStartOffset}`
        : ` - ${destPartPathOption.title}`) : '';

    const unitOption = unitOptions.find(x => x.value === unit)
    const formattedSize = unitOption ? (srcPartPath === 'custom'
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

    const getInfo = React.useCallback(async (target, path, byteswap) => {
        updateTarget = target;
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
            // default start offset, size and unit set to largest comparable size
            setSize(0)
            setUnit('bytes')

            // no media, reset
            if (isNil(media)) {
                switch(updateTarget) {
                    case 'Source':
                        setSrcStartOffset(0)
                        setSrcPartPath(null)
                        setSrcPartPathOptions([])
                        break;
                    case 'Destination':
                        setDestStartOffset(0)
                        setDestPartPath(null)
                        setDestPartPathOptions([])
                        break;
                    default:
                        console.error('Invalid update target', updateTarget)
                        break;
                }

                return
            }

            const newPartPathOptions = getPartPathOptions({media});

            switch(updateTarget)
            {
                case 'Source':
                    setSourceMedia(media);
                    setSrcStartOffset(0);
                    setSrcPartPath(newPartPathOptions.length > 0 ? newPartPathOptions[0].value : null);
                    setSrcPartPathOptions(newPartPathOptions);
                    break;
                case 'Destination':
                    setDestinationMedia(media);
                    setDestStartOffset(0);
                    setDestPartPath(newPartPathOptions.length > 0 ? newPartPathOptions[0].value : null);
                    setDestPartPathOptions(newPartPathOptions);
                    break;
                default:
                    console.error('Invalid update target', updateTarget)
                    break;
            }
        });

        newConnection.on('List', async (medias) => {
            setMedias(medias || [])

            const newMedia = getMedia({medias: medias, path: sourcePath});
            if (!newMedia) {
                return;
            }

            const newPath = get(newMedia, 'path');

            switch (updateTarget) {
                case 'Source':
                    setSourcePath(newPath)
                    setSourceMedia(newMedia)
                    await getInfo('Source', newPath, byteswap)
                    break
                case 'Destination':
                    setDestinationPath(newPath)
                    setDestinationMedia(newMedia)
                    await getInfo('Destination', newPath, false)
                    break
                default:
                    console.error('Invalid update target', updateTarget)
                    break;
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
    }, [backendBaseUrl, byteswap, connection, destinationType, getInfo, getMedia, setConnection, sourcePath, sourceType])
    
    const handleCompare = async () => {
        await backendApi.startCompare({
            title: `Comparing source ${sourceTypeFormatted} '${isNil(sourceMedia) ? sourcePath : sourceMedia.name}${formattedSrcPartPath}' and destination ${destinationTypeFormatted} '${isNil(destinationMedia) ? destinationPath : destinationMedia.name}${formattedDestPartPath}'${formattedSize}`,
            sourcePath: isNil(srcPartPath) || srcPartPath === 'custom' ? sourcePath : srcPartPath,
            sourceStartOffset: srcStartOffset,
            destinationPath: isNil(destPartPath) || destPartPath === 'custom' ? destinationPath : destPartPath,
            destinationStartOffset: destStartOffset,
            size: (size * unitOption.size),
            byteswap
        });
    }

    const handleConfirm = async (confirmed) => {
        setOpenConfirm(false)
        if (!confirmed) {
            return
        }
        await handleCompare()
    }

    const handleCancel = () => {
        if (connection) {
            connection.stop()
        }
        setOpenConfirm(false)
        setSourceType('ImageFile')
        setSourcePath(null)
        setSourceMedia(null)
        setSrcStartOffset(0)
        setSrcPartPath(null)
        setSrcPartPathOptions([])
        setByteswap(false)
        setMedias(null)
        setSize(0)
        setUnit('bytes')
        setDestinationPath(null)
        setDestStartOffset(0)
        setDestPartPath(null)
        setDestPartPathOptions([])
        setConnection(null)
    }
    
    const handleUpdate = async () => {
        await backendApi.updateList()
    }

    const compareDisabled = isNil(sourceMedia) || isNil(destinationMedia)
    
    return (
        <Box>
            <ConfirmDialog
                id="confirm-compare"
                open={openConfirm}
                title="Compare"
                description={`Do you want to compare '${isNil(sourceMedia) ? sourcePath : sourceMedia.name}${formattedSrcPartPath}' and '${isNil(destinationMedia) ? destinationPath : destinationMedia.name}${formattedDestPartPath}'${formattedSize}?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />
            <Title
                text="Compare"
                description="Compare an image file and a physical disk byte by byte."
            />
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
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
                            setSrcPartPath(null);
                            setSrcPartPathOptions([]);
                            if (value !== 'PhysicalDisk') {
                                return
                            }
                            updateTarget = 'Source';
                            getMedias();
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
                <Grid item xs={12} lg={6}>
                    {sourceType === 'ImageFile' && (
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
                                    title="Select source file"
                                    onChange={async (path) => {
                                        if (sourceMedia) {
                                            setSourceMedia(null)
                                        }
                                        setSourcePath(path);
                                        await getInfo('Source', path, byteswap);
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
                                await getInfo('Source', sourcePath, byteswap)
                            }}
                        />
                    )}
                    {sourceType === 'PhysicalDisk' && (
                        <Grid item xs={12} lg={12}>
                            <Stack direction="row">
                                <MediaSelectField
                                    id="source-media-path"
                                    label={
                                        <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                            <FontAwesomeIcon icon="hdd" style={{marginRight: '5px'}} /> Source physical disk
                                        </div>
                                    }
                                    medias={medias || []}
                                    path={sourcePath || ''}
                                    onChange={async (media) => {
                                        setSourcePath(media)
                                        setSourceMedia(media.path)
                                        await getInfo('Source', media.path, byteswap)
                                    }}                        
                                />
                                <IconButton
                                    aria-label="refresh"
                                    color="primary"
                                    disableFocusRipple={true}
                                    onClick={async () => {
                                        updateTarget = 'Source';
                                        handleUpdate()
                                    }}
                                >
                                    <FontAwesomeIcon icon="sync-alt" />
                                </IconButton>
                            </Stack>
                        </Grid>
                    )}
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label={
                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                <FontAwesomeIcon icon="download" style={{marginRight: '5px'}} /> Destination
                            </div>
                        }
                        id="destination"
                        emptyLabel="None available"
                        value={destinationType || 'ImageFile' }
                        options={typeOptions}
                        onChange={(value) => {
                            setDestinationType(value);
                            setDestinationPath(null);
                            setDestinationMedia(null);
                            setDestPartPath(null);
                            setDestPartPathOptions([]);
                            if (value === 'PhysicalDisk') {
                                updateTarget = 'Destination';
                                getMedias();
                            }
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
                <Grid item xs={12} lg={6}>
                    {destinationType === 'ImageFile' && (
                        <TextField
                            id="destination-path"
                            label={
                                <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                    <FontAwesomeIcon icon="file" style={{marginRight: '5px'}} /> Destination image file
                                </div>
                            }
                            value={destinationPath || ''}
                            endAdornment={
                                <BrowseOpenDialog
                                    id="browse-destination-path"
                                    title="Select destination image file"
                                    onChange={async (path) => {
                                        setDestinationPath(path)
                                        setDestStartOffset(0);
                                        setDestPartPath(null);
                                        setDestPartPathOptions([])
                                        await getInfo('Destination', path, byteswap)
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
                            onChange={async (event) => setDestinationPath(get(event, 'target.value'))}
                            onKeyDown={async (event) => {
                                if (event.key !== 'Enter') {
                                    return
                                }
                                setDestStartOffset(0);
                                setDestPartPath(null);
                                setDestPartPathOptions([])
                                await getInfo('Destination', destinationPath, byteswap)
                            }}
                        />
                    )}
                    {destinationType === 'PhysicalDisk' && (
                        <Grid item xs={12} lg={12}>
                            <Stack direction="row">
                                <MediaSelectField
                                    id="destination-media-path"
                                    label={
                                        <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                            <FontAwesomeIcon icon="hdd" style={{marginRight: '5px'}} /> Destination physical disk
                                        </div>
                                    }
                                    medias={medias || []}
                                    path={destinationPath || ''}
                                    onChange={async (media) => {
                                        setDestinationPath(media.path)
                                        setDestinationMedia(null)
                                        await getInfo('Destination', media.path, false)
                                    }}
                                />
                                    <IconButton
                                        aria-label="refresh"
                                        color="primary"
                                        disableFocusRipple={true}
                                        onClick={async () => {
                                            updateTarget = 'Destination';
                                            handleUpdate()
                                        }}
                                    >
                                    <FontAwesomeIcon icon="sync-alt" />
                                </IconButton>
                            </Stack>
                        </Grid>
                    )}
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
                                            <FontAwesomeIcon icon="file-fragment" style={{marginRight: '5px'}} /> {`Part of source ${sourceTypeFormatted} to compare`}
                                        </div>
                                    }
                                    id="read-part-path"
                                    disabled={isNil(sourceMedia)}
                                    emptyLabel="None available"
                                    value={srcPartPath || ''}
                                    options={srcPartPathOptions || []}
                                    onChange={(value) => {
                                        setSrcPartPath(value)
                                        setSrcStartOffset(0);
                                        console.log(sourceMedia);
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
                                            <FontAwesomeIcon icon="location-crosshairs" style={{marginRight: '5px'}} /> Source start offset
                                        </div>
                                    }
                                    id="src-start-offset"
                                    disabled={srcPartPath !== 'custom'}
                                    type={"number"}
                                    value={srcStartOffset}
                                    inputProps={{min: 0, style: { textAlign: 'right' }}}
                                    onChange={(event) => setSrcStartOffset(event.target.value)}
                                />
                            </Grid>
                        </Grid>
                        <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
                            <Grid item xs={12} lg={6}>
                                <SelectField
                                    label={
                                        <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                            <FontAwesomeIcon icon="file-fragment" style={{marginRight: '5px'}} /> {`Part of destination ${destinationTypeFormatted} to compare`}
                                        </div>
                                    }
                                    id="dest-part-path"
                                    disabled={isNil(destinationMedia)}
                                    emptyLabel="None available"
                                    value={destPartPath || ''}
                                    options={destPartPathOptions || []}
                                    onChange={(value) => {
                                        setDestPartPath(value);
                                        setDestStartOffset(0);
                                    }}
                                />
                            </Grid>
                        </Grid>
                        <Grid container spacing={1} direction="row" sx={{mt: 0.3}}>
                            <Grid item xs={12} lg={6}>
                                <TextField
                                    label={
                                        <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                            <FontAwesomeIcon icon="location-crosshairs" style={{marginRight: '5px'}} /> Destination start offset
                                        </div>
                                    }
                                    id="dest-start-offset"
                                    disabled={destPartPath !== 'custom'}
                                    type={"number"}
                                    value={destStartOffset}
                                    inputProps={{min: 0, style: { textAlign: 'right' }}}
                                    onChange={(event) => setDestStartOffset(event.target.value)}
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
                                    disabled={(srcPartPath !== 'custom' && destPartPath !== 'custom')}
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
                                    disabled={(srcPartPath !== 'custom' && destPartPath !== 'custom')}
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
                                            await getInfo('Source', sourceMedia.path, checked)
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
                        <Stack direction="row" spacing={1} sx={{mt: 1}}>
                            <RedirectButton
                                path="/"
                                icon="ban"
                                onClick={async () => handleCancel()}
                            >
                                Cancel
                            </RedirectButton>
                            <Button
                                disabled={compareDisabled}
                                icon="check"
                                onClick={async () => setOpenConfirm(true)}
                            >
                                Start compare
                            </Button>
                        </Stack>
                    </Box>
                </Grid>
            </Grid>
            {sourceMedia && sourceMedia.diskInfo && (
                <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
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