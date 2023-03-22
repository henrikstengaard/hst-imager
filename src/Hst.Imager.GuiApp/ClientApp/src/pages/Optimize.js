import React from 'react'
import Box from "@mui/material/Box";
import Title from "../components/Title";
import {get, isNil, trim} from "lodash";
import Grid from "@mui/material/Grid";
import TextField from "../components/TextField";
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import BrowseOpenDialog from "../components/BrowseOpenDialog";
import Stack from "@mui/material/Stack";
import RedirectButton from "../components/RedirectButton";
import Button from "../components/Button";
import ConfirmDialog from "../components/ConfirmDialog";
import SelectField from "../components/SelectField";
import {HubConnectionBuilder} from "@microsoft/signalr";
import Media from "../components/Media";
import Typography from "@mui/material/Typography";

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

export default function Optimize() {
    const [confirmOpen, setConfirmOpen] = React.useState(false);
    const [media, setMedia] = React.useState(null)
    const [size, setSize] = React.useState(0)
    const [unit, setUnit] = React.useState('bytes')
    const [path, setPath] = React.useState(null)
    const [prefillSize, setPrefillSize] = React.useState(null)
    const [prefillSizeOptions, setPrefillSizeOptions] = React.useState([])
    const [connection, setConnection] = React.useState(null);

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
                        
                        // no media, reset
                        if (isNil(media)) {
                            setSize(0)
                            setUnit('bytes')
                            setPrefillSize(null)
                            setPrefillSizeOptions(null)
                            return
                        }
                        
                        // default set media disk size
                        setSize(media.diskSize)
                        setUnit('bytes')
                        
                        // get and sort partition tables
                        const partitionTables = get(media, 'diskInfo.partitionTables') || []

                        // add select prefill option, if any partition tables are present                        
                        const newPrefillSizeOptions = []
                        if (partitionTables.length > 0) {
                            newPrefillSizeOptions.push({
                                title: 'Select size to prefill',
                                value: 'prefill'
                            })
                        }
                        
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
    }, [connection, setMedia, setPrefillSize, setPrefillSizeOptions, setUnit, setSize])
    
    const getInfo = async (path) => {
        const response = await fetch('api/info', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                sourceType: 'ImageFile',
                path
            })
        });
        if (!response.ok) {
            console.error('Failed to get info')
        }
    }
    
    const handleOptimize = async () => {
        const unitOption = unitOptions.find(x => x.value === unit)
        const response = await fetch('api/optimize', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                title: `Optimizing image file '${path}'`,
                path,
                size: (size * unitOption.size),
            })
        });
        if (!response.ok) {
            console.error('Failed to optimize image')
        }
    }

    const handleCancel = () => {
        if (connection) {
            connection.stop()
        }
        setConfirmOpen(false)
        setMedia(null)
        setSize(0)
        setUnit('bytes')
        setPath(null)
        setPrefillSizeOptions(null)
        setConnection(null)
    }

    const handleConfirm = async (confirmed) => {
        setConfirmOpen(false)
        if (!confirmed) {
            return
        }
        await handleOptimize()
    }

    const handleUpdate = async () => {
        await getInfo(path)
    }
    
    const unitOption = isNil(unit) ? null : unitOptions.find(x => x.value === unit)
    const updateDisabled = isNil(path) || trim(path) === ''
    const optimizeDisabled = updateDisabled || isNil(media)
    
    return (
        <Box>
            <ConfirmDialog
                id="confirm-optimize"
                open={confirmOpen}
                title="Optimize"
                description={`Do you want to optimize image file '${path}' to size ${size} ${get(unitOption, 'title')}?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />
            <Title
                text="Optimize"
                description="Optimize image file size."
            />
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
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
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label="Prefill size to optimize"
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
                <Grid item xs={12} lg={6}>
                    <Box display="flex" justifyContent="flex-end">
                        <Stack direction="row" spacing={2} sx={{mt: 2}}>
                            <RedirectButton
                                path="/"
                                icon="ban"
                                onClick={async () => handleCancel()}
                            >
                                Cancel
                            </RedirectButton>
                            <Button
                                disabled={updateDisabled}
                                icon="sync-alt"
                                onClick={async () => handleUpdate()}
                            >
                                Update
                            </Button>
                            <Button
                                disabled={optimizeDisabled}
                                icon="magic"
                                onClick={async () => setConfirmOpen(true)}
                            >
                                Optimize image
                            </Button>
                        </Stack>
                    </Box>
                </Grid>
            </Grid>
            {media && media.diskInfo && (
                <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                    <Grid item xs={12}>
                        <Typography variant="h3">
                            Source file
                        </Typography>
                        <Typography>
                            Disk information read from source file.
                        </Typography>
                        <Media media={media}/>
                    </Grid>
                </Grid>
            )}            
        </Box>
    )
}