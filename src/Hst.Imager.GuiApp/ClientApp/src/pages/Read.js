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
import {Api} from "../utils/Api";
import {HubConnectionBuilder} from "@microsoft/signalr";
import Typography from "@mui/material/Typography";
import CheckboxField from "../components/CheckboxField";
import SelectField from "../components/SelectField";

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

export default function Read() {
    const [confirmOpen, setConfirmOpen] = React.useState(false);
    const [sourceMedia, setSourceMedia] = React.useState(null)
    const [medias, setMedias] = React.useState(null)
    const [size, setSize] = React.useState(0)
    const [unit, setUnit] = React.useState('bytes')
    const [destinationPath, setDestinationPath] = React.useState(null)
    const [verify, setVerify] = React.useState(false)
    const [force, setForce] = React.useState(false)
    const [retries, setRetries] = React.useState(5)
    const [prefillSize, setPrefillSize] = React.useState(null)
    const [prefillSizeOptions, setPrefillSizeOptions] = React.useState([])
    const [connection, setConnection] = React.useState(null);

    const api = React.useMemo(() => new Api(), []);

    const unitOption = unitOptions.find(x => x.value === unit)
    const formattedSize = size === 0 ? 'entire disk size' : `size ${size} ${unitOption.title}`
    
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
                sourceType: 'PhysicalDisk',
                path
            })
        });
        if (!response.ok) {
            console.error('Failed to get info')
        }
    }
    
    // get medias
    React.useEffect(() => {
        if (medias !== null) {
            return
        }
        handleGetMedias()
    }, [medias, handleGetMedias])

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
                        setSourceMedia(media)

                        // default set size and unit to largest comparable size
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
                            title: 'Entire disk',
                            value: 0
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
                        const newPath = getPath({medias: medias, path: get(sourceMedia, 'path')})
                        const newMedia = newPath ? medias.find(x => x.path === newPath) : null

                        setMedias(medias || [])
                        if (newMedia) {
                            setSourceMedia(newMedia)
                            await getInfo(newMedia.path)
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
    }, [connection, getPath, setMedias, setSourceMedia, sourceMedia, setPrefillSize, setPrefillSizeOptions,
        setUnit, setSize])
    
    const handleRead = async () => {
        const response = await fetch('api/read', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                title: `Reading disk '${sourceMedia.name}' to file '${destinationPath}' with ${formattedSize}`,
                sourcePath: sourceMedia.path,
                destinationPath,
                size: (size * unitOption.size),
                retries,
                verify,
                force
            })
        });
        if (!response.ok) {
            console.error('Failed to read')
        }
    }

    const handleConfirm = async (confirmed) => {
        setConfirmOpen(false)
        if (!confirmed) {
            return
        }
        await handleRead()
    }

    const handleUpdate = async () => {
        await api.list()
    }

    const handleCancel = () => {
        if (connection) {
            connection.stop()
        }
        setConfirmOpen(false)
        setSourceMedia(null)
        setMedias(null)
        setSize(0)
        setUnit('bytes')
        setDestinationPath(null)
        setVerify(false)
        setForce(false)
        setRetries(5)
        setPrefillSize(null)
        setPrefillSizeOptions([])
        setConnection(null)
    }
    
    const readDisabled = isNil(sourceMedia) || isNil(destinationPath)

    return (
        <Box>
            <ConfirmDialog
                id="confirm-read"
                open={confirmOpen}
                title="Read"
                description={`Do you want to read disk '${sourceMedia === null ? '' : sourceMedia.name}' to file '${destinationPath}' with ${formattedSize}?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />
            <Title
                text="Read"
                description="Read physical disk to image file."
            />
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12} lg={6}>
                    <MediaSelectField
                        label={
                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                <FontAwesomeIcon icon="hdd" style={{marginRight: '5px'}} /> Source disk
                            </div>
                        }
                        id="source-disk"
                        medias={medias || []}
                        path={get(sourceMedia, 'path') || ''}
                        onChange={(media) => setSourceMedia(media)}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12} lg={6}>
                    <TextField
                        id="destination-file"
                        label={
                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                <FontAwesomeIcon icon="file" style={{marginRight: '5px'}} /> Destination file
                            </div>
                        }
                        value={destinationPath || ''}
                        endAdornment={
                            <BrowseSaveDialog
                                id="read-destination-path"
                                title="Select destination image"
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
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label="Prefill size to read"
                        id="prefill-size"
                        emptyLabel="None available"
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
                        type="number"
                        value={size}
                        inputProps={{min: 0, style: { textAlign: 'right' }}}
                        onChange={(event) => setSize(event.target.value)}
                        onKeyDown={async (event) => {
                            if (event.key !== 'Enter') {
                                return
                            }
                            setConfirmOpen(true)
                        }}
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
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
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
                <Grid item xs={12}>
                    <CheckboxField
                        id="verify"
                        label="Verify while reading"
                        value={verify}
                        onChange={(checked) => setVerify(checked)}
                    />
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
                                disabled={readDisabled}
                                icon="upload"
                                onClick={async () => setConfirmOpen(true)}
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
                            Source disk
                        </Typography>
                        <Typography>
                            Disk information read from source disk.
                        </Typography>
                        <Media media={sourceMedia}/>
                    </Grid>
                </Grid>
            )}
        </Box>
    )
}