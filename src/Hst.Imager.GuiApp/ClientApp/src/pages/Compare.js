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

const formatPartitionTableType = (partitionTableType) => {
    switch (partitionTableType) {
        case 'GuidPartitionTable':
            return 'Guid Partition Table'
        case 'MasterBootRecord':
            return 'Master Boot Record'
        case 'RigidDiskBlock':
            return 'Rigid Disk Block'
        default:
            return ''
    }
}

export default function Verify() {
    const [openConfirm, setOpenConfirm] = React.useState(false);
    const [sourceMedia, setSourceMedia] = React.useState(null)
    const [sourcePath, setSourcePath] = React.useState(null)
    const [byteswap, setByteswap] = React.useState(false);
    const [medias, setMedias] = React.useState(null)
    const [size, setSize] = React.useState(0)
    const [unit, setUnit] = React.useState('bytes')
    const [destinationPath, setDestinationPath] = React.useState(null)
    const [sourceType, setSourceType] = React.useState('ImageFile')
    const [compareAll, setCompareAll] = React.useState(true);
    const [prefillSize, setPrefillSize] = React.useState(null)
    const [prefillSizeOptions, setPrefillSizeOptions] = React.useState([])
    const [connection, setConnection] = React.useState(null);
    const {
        backendBaseUrl,
        backendApi
    } = React.useContext(BackendApiStateContext)

    const unitOption = unitOptions.find(x => x.value === unit)
    const formattedSize = size === 0 ? '' : ` with size ${size} ${unitOption.title}`

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

    const getInfo = React.useCallback(async (path, sourceType, byteswap) => {
        await backendApi.updateInfo({ path, sourceType, byteswap });
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

            // default set size and unit to largest comparable size
            setCompareAll(true)
            setSize(0)
            setUnit('bytes')

            // no media, reset
            if (isNil(media)) {
                setPrefillSize(null)
                setPrefillSizeOptions([])
                return
            }

            // get and sort partition tables
            const partitionTables = get(media, 'diskInfo.partitionTables') || []

            // add select prefill option, if any partition tables are present                        
            const newPrefillSizeOptions = [{
                title: 'Select size to prefill',
                value: 'prefill'
            },{
                title: `Disk (${media.diskSize} bytes)`,
                value: media.diskSize
            }]

            // add partition tables as prefill size options
            for (let i = 0; i < partitionTables.length; i++) {
                const partitionTableSize = get(partitionTables[i], 'size') || 0
                newPrefillSizeOptions.push({
                    title: `${formatPartitionTableType(partitionTables[i].type)} (${partitionTableSize} bytes)`,
                    value: partitionTableSize
                })
            }

            setPrefillSize(newPrefillSizeOptions.length > 0 ? 'prefill' : null)
            setPrefillSizeOptions(newPrefillSizeOptions)
        });

        newConnection.on('List', async (medias) => {
            const newMedia = getMedia({medias: medias, path: sourcePath});
            const newPath = get(newMedia, 'path');

            setMedias(medias || [])
            if (newMedia) {
                setSourcePath(newPath)
                setSourceMedia(newMedia)
                await getInfo(newPath, sourceType, byteswap)
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

    const handleSourceTypeChange = async (value) => {
        setSourceType(value)
        switch (value) {
            case 'PhysicalDisk':
                if (medias === null) {
                    setSourcePath(null)
                    setSourceMedia(null)
                    await getMedias()
                    return
                }

                const newMedia = getMedia({medias: medias, path: sourcePath});
                const newPath = get(newMedia, 'path');

                setSourcePath(newPath)
                setSourceMedia(newMedia)
                if (value === 'PhysicalDisk' && newMedia) {
                    await getInfo(newPath, sourceType, byteswap)
                }

                break
            default:
                setSourcePath(null)
                setSourceMedia(null)
                break
        }
    }
    
    const handleCompare = async () => {
        await backendApi.startCompare({
            title: `Comparing ${(sourceType === 'ImageFile' ? 'file' : 'disk')} '${isNil(sourceMedia) ? sourcePath : sourceMedia.name}' and file '${destinationPath}'${formattedSize}`,
            sourceType,
            sourcePath,
            destinationPath,
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
        setSourceMedia(null)
        setSourcePath(null)
        setByteswap(false)
        setMedias(null)
        setSize(0)
        setUnit('bytes')
        setDestinationPath(null)
        setSourceType('ImageFile')
        setPrefillSize(null)
        setPrefillSizeOptions([])
        setConnection(null)
    }
    
    const handleUpdate = async () => {
        await backendApi.updateList()
    }

    const compareDisabled = isNil(sourcePath) || isNil(destinationPath)
    
    return (
        <Box>
            <ConfirmDialog
                id="confirm-compare"
                open={openConfirm}
                title="Compare"
                description={`Do you want to compare '${isNil(sourceMedia) ? sourcePath : sourceMedia.name}' and '${destinationPath}'${formattedSize}?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />
            <Title
                text="Compare"
                description="Compare an image file or physical disk against an image file comparing them byte by byte."
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
                            onChange={(event) => handleSourceTypeChange(event.target.value)}
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
                                        await getInfo(path, sourceType, byteswap)
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
                                await getInfo(sourcePath, sourceType, byteswap)
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
                                await getInfo(media.path, sourceType, byteswap)
                            }}                        
                        />
                    )}
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
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
                                fileFilters = {[{
                                    name: 'Hard disk image files',
                                    extensions: ['img', 'hdf', 'vhd', 'xz', 'gz', 'zip', 'rar']
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
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0}}>
                <Grid item xs={12}>
                    <CheckboxField
                        id="byteswap"
                        label="Byteswap source sectors"
                        value={byteswap}
                        onChange={async (checked) => {
                            setByteswap(checked)
                            if (sourceMedia) {
                                await getInfo(sourceMedia.path, sourceType, checked)
                            }
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0}}>
                <Grid item xs={12}>
                    <CheckboxField
                        id="compare-all"
                        label="Largest comparable size (smallest of source and destination)"
                        value={compareAll}
                        onChange={(checked) => {
                            setSize(checked ? 0 : get(sourceMedia, 'diskSize') || 0)
                            setUnit('bytes')
                            setCompareAll(checked)
                        }}
                    />
                </Grid>
            </Grid>
            {!compareAll && (
                <React.Fragment>
                    <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0}}>
                        <Grid item xs={12} lg={6}>
                            <SelectField
                                label="Prefill size to compare"
                                id="prefill-size"
                                emptyLabel="None available"
                                disabled={compareAll}
                                value={prefillSize || ''}
                                options={prefillSizeOptions || []}
                                onChange={(value) => {
                                    setSize(value)
                                    setUnit('bytes')
                                }}
                            />
                        </Grid>
                    </Grid>
                    <Grid container spacing={1} direction="row" sx={{mt: 1}}>
                        <Grid item xs={8} lg={4}>
                            <TextField
                                label="Size"
                                id="size"
                                type={compareAll ? "text" : "number"}
                                disabled={compareAll}
                                value={compareAll ? '' : size}
                                inputProps={{min: 0, style: { textAlign: 'right' }}}
                                onChange={(event) => setSize(event.target.value)}
                                onKeyDown={async (event) => {
                                    if (event.key !== 'Enter') {
                                        return
                                    }
                                    setOpenConfirm(true)
                                }}
                            />
                        </Grid>
                        <Grid item xs={4} lg={2}>
                            <SelectField
                                label="Unit"
                                id="unit"
                                disabled={compareAll}
                                value={unit || ''}
                                options={unitOptions}
                                onChange={(value) => setUnit(value)}
                            />
                        </Grid>
                    </Grid>
                </React.Fragment>
            )}
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