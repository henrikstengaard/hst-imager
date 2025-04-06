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

            const newPartPathOptions = getPartPathOptions(media);

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
                    await getInfo('Source', newPath, sourceType, byteswap)
                    break
                case 'Destination':
                    setDestinationPath(newPath)
                    setDestinationMedia(newMedia)
                    await getInfo('Destination', newPath, destinationType, false)
                    break
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
    }, [backendBaseUrl, byteswap, connection, getInfo, getMedia, setConnection, sourcePath, sourceType])
    
    const handleCompare = async () => {
        await backendApi.startCompare({
            title: `Comparing source ${(sourceType === 'ImageFile' ? 'file' : 'disk')} '${isNil(sourceMedia) ? sourcePath : sourceMedia.name}${formattedSrcPartPath}' and destination ${(destinationType === 'ImageFile' ? 'file' : 'disk')}  '${isNil(destinationMedia) ? destinationPath : destinationMedia.name}${formattedDestPartPath}'${formattedSize}`,
            comparePhysicalDisk: sourceType === 'PhysicalDisk' || destinationType === 'PhysicalDisk',
            sourceType,
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
                description="Compare an image file or physical disk against an image file comparing them byte by byte."
            />
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label="Source"
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
                                    <FontAwesomeIcon icon="file" style={{marginRight: '5px'}} /> Source file
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
                                        await getInfo('Source', path, sourceType, byteswap);
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
                                await getInfo('Source', sourcePath, sourceType, byteswap)
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
                            medias={medias || []}
                            path={sourcePath || ''}
                            onChange={async (media) => {
                                setSourcePath(media)
                                setSourceMedia(media.path)
                                await getInfo('Source', media.path, sourceType, byteswap)
                            }}                        
                        />
                    )}
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label={`Part of source ${sourceType === 'PhysicalDisk' ? 'physical disk' : 'image file'} to compare against`}
                        id="read-part-path"
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
            {srcPartPath === 'custom' && (
                    <Grid container spacing={1} direction="row" sx={{mt: 0.3}}>
                        <Grid item xs={12} lg={6}>
                            <TextField
                                label="Source start offset"
                                id="src-start-offset"
                                type={"number"}
                                value={srcStartOffset}
                                inputProps={{min: 0, style: { textAlign: 'right' }}}
                                onChange={(event) => setSrcStartOffset(event.target.value)}
                            />
                        </Grid>
                    </Grid>
            )}
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label="Destination"
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
                                    <FontAwesomeIcon icon="file" style={{marginRight: '5px'}} /> Destination file
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
                            onChange={async (event) => setDestinationPath(get(event, 'target.value'))}
                            onKeyDown={async (event) => {
                                if (event.key !== 'Enter') {
                                    return
                                }
                                setDestStartOffset(0);
                                setDestPartPath(null);
                                setDestPartPathOptions([])
                                await getInfo('Destination', destinationPath, 'ImageFile', byteswap)
                            }}
                        />
                    )}
                    {destinationType === 'PhysicalDisk' && (
                        <MediaSelectField
                            id="destination-media-path"
                            label={
                                <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                    <FontAwesomeIcon icon="hdd" style={{marginRight: '5px'}} /> Destination disk
                                </div>
                            }
                            medias={medias || []}
                            path={destinationPath || ''}
                            onChange={async (media) => {
                                setDestinationPath(media.path)
                                setDestinationMedia(null)
                                await getInfo('Destination', media.path, destinationType, false)
                            }}
                        />
                    )}
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label={`Part of destination image file to compare against`}
                        id="dest-part-path"
                        emptyLabel="None available"
                        value={destPartPath || ''}
                        options={destPartPathOptions || []}
                        onChange={(value) => {
                            setDestPartPath(value);
                            setDestStartOffset(0);
                            //setSize(value === 'custom' ? get(sourceMedia, 'diskSize') || 0 : 0)
                            //setUnit('bytes')
                        }}
                    />
                </Grid>
            </Grid>
            {destPartPath === 'custom' && (
                <Grid container spacing={1} direction="row" sx={{mt: 0.3}}>
                    <Grid item xs={12} lg={6}>
                        <TextField
                            label="Destination start offset"
                            id="dest-start-offset"
                            type={"number"}
                            value={destStartOffset}
                            inputProps={{min: 0, style: { textAlign: 'right' }}}
                            onChange={(event) => setDestStartOffset(event.target.value)}
                        />
                    </Grid>
                </Grid>
            )}
            {(srcPartPath === 'custom' || destPartPath === 'custom') && (
                <React.Fragment>
                    <Grid container spacing={1} direction="row" sx={{mt: 0.3}}>
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
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0}}>
                <Grid item xs={12}>
                    <CheckboxField
                        id="byteswap"
                        label="Byteswap source sectors"
                        value={byteswap}
                        onChange={async (checked) => {
                            setByteswap(checked)
                            if (sourceMedia) {
                                await getInfo('Source', sourceMedia.path, sourceType, checked)
                            }
                        }}
                    />
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
                                icon="sync-alt"
                                onClick={async () => handleUpdate()}
                            >
                                Update
                            </Button>
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