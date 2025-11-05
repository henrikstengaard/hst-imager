import React from 'react'
import {get, isNil} from "lodash";
import Box from "@mui/material/Box";
import Title from "../components/Title";
import Grid from "@mui/material/Grid";
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import TextField from "../components/TextField";
import Stack from "@mui/material/Stack";
import RedirectButton from "../components/RedirectButton";
import Button from "../components/Button";
import BrowseOpenDialog from "../components/BrowseOpenDialog";
import ConfirmDialog from "../components/ConfirmDialog";
import Typography from "@mui/material/Typography";
import Media from "../components/Media";
import {HubConnectionBuilder} from "@microsoft/signalr";
import CheckboxField from "../components/CheckboxField";
import {BackendApiStateContext} from "../components/BackendApiContext";
import Accordion from "../components/Accordion";
import SelectField from "../components/SelectField";
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

export default function Transfer() {
    const [openConfirm, setOpenConfirm] = React.useState(false)
    const [sourceMedia, setSourceMedia] = React.useState(null)
    const [sourcePath, setSourcePath] = React.useState(null)
    const [byteswap, setByteswap] = React.useState(false);
    const [destinationMedia, setDestinationMedia] = React.useState(null)
    const [destinationPath, setDestinationPath] = React.useState(null)
    const [size, setSize] = React.useState(0)
    const [unit, setUnit] = React.useState('bytes')
    const [srcStartOffset, setSrcStartOffset] = React.useState(0);
    const [srcPartPath, setSrcPartPath] = React.useState(null)
    const [srcPartPathOptions, setSrcPartPathOptions] = React.useState([])
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
    const formattedSize = unitOption ? (srcPartPath === 'custom' || destPartPath === 'custom'
        ? ` with size ${size} ${unitOption.title}`
        : '') : '';

    const srcMediaName = get(sourceMedia, 'name') || (isNil(sourcePath) ? '' : sourcePath.replace(/^.*[\\/]/, ''));
    const destMediaName = get(destinationMedia, 'name') || (isNil(destinationPath) ? '' : destinationPath.replace(/^.*[\\/]/, ''));
    
    const handleCancel = () => {
        setOpenConfirm(false);
        setSourceMedia(null)
        setSourcePath(null)
        setByteswap(false)
        setSize(0)
        setUnit('bytes')
        setSrcStartOffset(0);
        setSrcPartPath(null)
        setSrcPartPathOptions([])
        setDestinationMedia(null)
        setDestinationPath(null)
        setDestStartOffset(0);
        setDestPartPath(null)
        setDestPartPathOptions([])
    }

    const getInfo = React.useCallback(async (target, path, byteswap, allowNonExisting) => {
        updateTarget = target;
        await backendApi.updateInfo({ path, byteswap, allowNonExisting });
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
                setSrcStartOffset(0)
                setSize(0)
                setUnit('bytes')

                // no media, reset
                if (isNil(media)) {
                    setSrcPartPath(null)
                    setSrcPartPathOptions([])
                    return
                }

                const newPartPathOptions = getPartPathOptions({media});

                setSrcPartPath(newPartPathOptions.length > 0 ? newPartPathOptions[0].value : null)
                setSrcPartPathOptions(newPartPathOptions)
                return
            }

            if (updateTarget === 'Destination') {
                setDestinationMedia(media);
                setDestStartOffset(0)

                // no media, reset
                if (isNil(media)) {
                    setDestPartPath(null)
                    setDestPartPathOptions([])
                    return
                }

                const newPartPathOptions = getPartPathOptions({media});

                setDestPartPath(newPartPathOptions.length > 0 ? newPartPathOptions[0].value : null)
                setDestPartPathOptions(newPartPathOptions)
            }
        });

        newConnection.start();

        setConnection(newConnection);

        return () => {
            if (!connection) {
                return
            }

            connection.stop();
        };
    }, [backendApi, backendBaseUrl, byteswap, connection, getInfo, setConnection, sourceMedia])
    
    const handleTransfer = async () => {
        await backendApi.startTransfer({
            title: `Transferring source image file '${srcMediaName}${formattedSrcPartPath}' to destination image file '${destMediaName}${formattedDestPartPath}'${formattedSize}`,
            sourcePath: isNil(srcPartPath) || srcPartPath === 'custom' ? sourceMedia.path : srcPartPath,
            destinationPath: isNil(destPartPath) || destPartPath === 'custom' ? destinationPath : destPartPath,
            srcStartOffset,
            destStartOffset,
            size: (size * unitOption.size),
            byteswap
        });
    }
    
    const handleConfirm = async (confirmed) => {
        setOpenConfirm(false)
        if (!confirmed) {
            return
        }
        await handleTransfer()
    }

    const transferDisabled = isNil(sourceMedia) || isNil(destinationPath) || destinationPath === ''

    return (
        <Box>
            <ConfirmDialog
                id="confirm-transfer"
                open={openConfirm}
                title="Transfer"
                description={`Do you want to transfer source image file '${srcMediaName}${formattedSrcPartPath}' to destination image file '${destMediaName}${formattedDestPartPath}'${formattedSize}?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />            
            <Title
                text="Transfer"
                description="Transfer converts, imports or exports from an image file or part of to another."
            />
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
                <Grid item xs={12} lg={6}>
                    <TextField
                        id="source-path"
                        label={
                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                <FontAwesomeIcon icon="upload" style={{marginRight: '5px'}} /> Source image file
                            </div>
                        }
                        value={sourcePath || ''}
                        endAdornment={
                            <BrowseOpenDialog
                                id="browse-source-path"
                                title="Select source image file"
                                onChange={async (path) => {
                                    setSourcePath(path)
                                    await getInfo('Source', path, byteswap, false)
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
                            updateTarget = 'Source';
                            setSourcePath(get(event, 'target.value'))
                            if (sourceMedia) {
                                setSourceMedia(null)
                            }
                        }}
                        onKeyDown={async (event) => {
                            if (event.key !== 'Enter') {
                                return
                            }
                            await getInfo('Source', sourcePath, byteswap, false)
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
                <Grid item xs={12} lg={6}>
                    <TextField
                        id="destination-path"
                        label={
                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                <FontAwesomeIcon icon="download" style={{marginRight: '5px'}} /> Destination image file
                            </div>
                        }
                        value={destinationPath || ''}
                        endAdornment={
                            <BrowseOpenDialog
                                id="browse-destination-path"
                                title="Select destination image file"
                                onChange={async (path) => {
                                    setDestinationPath(path)
                                    await getInfo('Destination', path, byteswap, true)
                                }}
                                fileFilters = {[{
                                    name: 'Hard disk image files',
                                    extensions: ['img', 'hdf', 'vhd', 'gz', 'zip']
                                }, {
                                    name: 'All files',
                                    extensions: ['*']
                                }]}
                                promptCreate={true}
                            />
                        }
                        onChange={(event) => {
                            updateTarget = 'Destination';
                            setDestinationPath(get(event, 'target.value'))
                            if (destinationMedia) {
                                setDestinationMedia(null)
                            }
                        }}
                        onKeyDown={async (event) => {
                            if (event.key !== 'Enter') {
                                return
                            }
                            await getInfo('Destination', sourcePath, byteswap, true)
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
                                            <FontAwesomeIcon icon="file-fragment" style={{marginRight: '5px'}} /> Part of source image file to read from
                                        </div>
                                    }
                                    id="src-part-path"
                                    disabled={isNil(sourceMedia)}
                                    emptyLabel="None available"
                                    value={srcPartPath || ''}
                                    options={srcPartPathOptions || []}
                                    onChange={(value) => {
                                        setSrcPartPath(value)
                                        setSrcStartOffset(0);
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
                                            <FontAwesomeIcon icon="file-fragment" style={{marginRight: '5px'}} /> Part of destination image file to write to
                                        </div>
                                    }
                                    id="dest-part-path"
                                    disabled={isNil(destinationMedia)}
                                    emptyLabel="None available"
                                    value={destPartPath || ''}
                                    options={destPartPathOptions || []}
                                    onChange={(value) => {
                                        setDestPartPath(value)
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
                                    disabled={srcPartPath !== 'custom' && destPartPath !== 'custom'}
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
                                    disabled={srcPartPath !== 'custom' && destPartPath !== 'custom'}
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
                                            await getInfo(sourceMedia.path, checked, false)
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
                                disabled={transferDisabled}
                                icon="exchange-alt"
                                onClick={async () => setOpenConfirm(true)}
                            >
                                Start transfer
                            </Button>
                        </Stack>
                    </Box>
                </Grid>
            </Grid>
            {sourceMedia && sourceMedia.diskInfo && (
                <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                    <Grid item xs={12}>
                        <Typography variant="h3">
                            Source file
                        </Typography>
                        <Typography>
                            Disk information read from source image file.
                        </Typography>
                        <Media media={sourceMedia}/>
                    </Grid>
                </Grid>
            )}
        </Box>
    )
}