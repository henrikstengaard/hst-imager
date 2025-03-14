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

export default function Write() {
    const [openConfirm, setOpenConfirm] = React.useState(false);
    const [sourceMedia, setSourceMedia] = React.useState(null)
    const [destinationMedia, setDestinationMedia] = React.useState(null)
    const [medias, setMedias] = React.useState(null)
    const [byteswap, setByteswap] = React.useState(false);
    const [size, setSize] = React.useState(0)
    const [unit, setUnit] = React.useState('bytes')
    const [sourcePath, setSourcePath] = React.useState(null)
    const [writeAll, setWriteAll] = React.useState(true);
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

    const getInfo = React.useCallback(async (path, byteswap) => {
        await backendApi.updateInfo({ path, sourceType: 'ImageFile', byteswap });
    }, [backendApi])

    React.useEffect(() => {
        if (connection) {
            return
        }

        getMedias()
        
        const newConnection = new HubConnectionBuilder()
            .withUrl(`${backendBaseUrl}hubs/result`)
            .withAutomaticReconnect()
            .build();

        newConnection.on("Info", (media) => {
            setSourceMedia(media)

            // default set size and unit to largest comparable size
            setWriteAll(true)
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
            const newMedia = getMedia({medias: medias, path: get(destinationMedia, 'path')});

            setMedias(medias || [])
            if (newMedia) {
                setDestinationMedia(newMedia)
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
    }, [backendBaseUrl, connection, destinationMedia, getMedia, getMedias, setConnection])
    
    const handleWrite = async () => {
        await backendApi.startWrite({
            title: `Writing file '${sourcePath}' to disk '${get(destinationMedia, 'name') || ''}'${formattedSize}`,
            sourcePath,
            destinationPath: destinationMedia.path,
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
        setSourceMedia(null)
        setDestinationMedia(null)
        setMedias([])
        setByteswap(false)
        setSize(0)
        setUnit('bytes')
        setSourcePath(null)
        setPrefillSize(null)
        setPrefillSizeOptions([])
        setConnection(null)
    }
    
    const writeDisabled = isNil(sourceMedia) || isNil(destinationMedia)

    return (
        <Box>
            <ConfirmDialog
                id="confirm-write"
                open={openConfirm}
                title="Write"
                description={`Do you want to write file '${sourcePath}' to disk '${get(destinationMedia, 'name') || ''}'${formattedSize}?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />
            <Title
                text="Write"
                description="Write image file to physical disk."
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
                            await getInfo(sourcePath, byteswap)
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
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
            <Grid container spacing={0} direction="row" alignItems="center" sx={{mt: 0}}>
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
            <Grid container spacing={0} direction="row" alignItems="center" sx={{mt: 0}}>
                <Grid item xs={12}>
                    <CheckboxField
                        id="write-all"
                        label="Write entire file"
                        value={writeAll}
                        onChange={(checked) => {
                            setSize(checked ? 0 : get(sourceMedia, 'diskSize') || 0)
                            setUnit('bytes')
                            setWriteAll(checked)
                        }}
                    />
                </Grid>
            </Grid>
            {!writeAll && (
                <React.Fragment>
                    <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0}}>
                        <Grid item xs={12} lg={6}>
                            <SelectField
                                label="Prefill size to write"
                                id="prefill-size"
                                emptyLabel="None available"
                                disabled={writeAll}
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
                                type={writeAll ? "text" : "number"}
                                disabled={writeAll}
                                value={writeAll ? '' : size}
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
                                disabled={writeAll}
                                value={unit || ''}
                                options={unitOptions}
                                onChange={(value) => setUnit(value)}
                            />
                        </Grid>
                    </Grid>
                </React.Fragment>
            )}
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