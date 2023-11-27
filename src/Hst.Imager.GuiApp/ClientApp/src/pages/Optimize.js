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
import CheckboxField from "../components/CheckboxField";
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

export default function Optimize() {
    const [openConfirm, setOpenConfirm] = React.useState(false)
    const [media, setMedia] = React.useState(null)
    const [byteswap, setByteswap] = React.useState(false);
    const [size, setSize] = React.useState(0)
    const [unit, setUnit] = React.useState('bytes')
    const [path, setPath] = React.useState(null)
    const [prefillSize, setPrefillSize] = React.useState(null)
    const [prefillSizeOptions, setPrefillSizeOptions] = React.useState([])
    const [connection, setConnection] = React.useState(null);
    const {
        backendBaseUrl,
        backendApi
    } = React.useContext(BackendApiStateContext)
    
    React.useEffect(() => {
        if (connection) {
            return
        }

        const newConnection = new HubConnectionBuilder()
            .withUrl(`${backendBaseUrl}hubs/result`)
            .withAutomaticReconnect()
            .build();

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

        newConnection.start();

        setConnection(newConnection);

        return () => {
            if (!connection) {
                return
            }

            connection.stop();
        };
    }, [backendBaseUrl, connection, setConnection])
    
    const getInfo = React.useCallback(async (path, byteswap) => {
        await backendApi.updateInfo({ path, sourceType: 'ImageFile', byteswap });
    }, [backendApi])

    const handleOptimize = async () => {
        const unitOption = unitOptions.find(x => x.value === unit)
        await backendApi.startOptimize({
            title: `Optimizing image file '${path}'`,
            path,
            size: (size * unitOption.size)
        });
    }

    const handleCancel = () => {
        if (connection) {
            connection.stop()
        }
        setOpenConfirm(false)
        setMedia(null)
        setByteswap(false)
        setSize(0)
        setUnit('bytes')
        setPath(null)
        setPrefillSizeOptions(null)
        setConnection(null)
    }

    const handleConfirm = async (confirmed) => {
        setOpenConfirm(false)
        if (!confirmed) {
            return
        }
        await handleOptimize()
    }

    const handleUpdate = async () => {
        await getInfo(path, byteswap)
    }
    
    const unitOption = isNil(unit) ? null : unitOptions.find(x => x.value === unit)
    const updateDisabled = isNil(path) || trim(path) === ''
    const optimizeDisabled = updateDisabled || isNil(media)
    
    return (
        <Box>
            <ConfirmDialog
                id="confirm-optimize"
                open={openConfirm}
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
                                    await getInfo(path, byteswap)
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
                            await getInfo(path, byteswap)
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12}>
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
                            setOpenConfirm(true)
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
                                onClick={async () => setOpenConfirm(true)}
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