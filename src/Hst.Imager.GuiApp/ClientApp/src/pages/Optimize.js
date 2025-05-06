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
import Accordion from "../components/Accordion";
import {formatBytes} from "../utils/Format";

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

export default function Optimize() {
    const [openConfirm, setOpenConfirm] = React.useState(false)
    const [media, setMedia] = React.useState(null)
    const [byteswap, setByteswap] = React.useState(false);
    const [size, setSize] = React.useState(0)
    const [unit, setUnit] = React.useState('bytes')
    const [path, setPath] = React.useState(null)
    const [optimizePartPath, setOptimizePartPath] = React.useState(null)
    const [optimizePartPathOptions, setOptimizePartPathOptions] = React.useState([])
    const [connection, setConnection] = React.useState(null);
    const {
        backendBaseUrl,
        backendApi
    } = React.useContext(BackendApiStateContext)

    const unitOption = isNil(unit) ? null : unitOptions.find(x => x.value === unit)
    const sizeFormatted = size > 0 ? ` to size ${size} ${get(unitOption, 'title')}` : '';

    React.useEffect(() => {
        if (connection) {
            return
        }

        const newConnection = new HubConnectionBuilder()
            .withUrl(`${backendBaseUrl}hubs/result`)
            .withAutomaticReconnect()
            .build();

        newConnection.on("Info", (newMedia) => {
            setMedia(newMedia)

            // default start offset, size and unit
            setSize(0)
            setUnit('bytes');

            // no media, reset
            if (isNil(newMedia)) {
                setOptimizePartPath(null)
                setOptimizePartPathOptions([])
                return
            }

            const newPartPathOptions = [{
                title: `Disk (${formatBytes(newMedia.diskSize)})`,
                value: newMedia.diskSize
            }]

            const gptPartitionTablePart = get(newMedia, 'diskInfo.gptPartitionTablePart');
            if (gptPartitionTablePart) {
                newPartPathOptions.push({
                    title: `Guid Partition Table (${formatBytes(gptPartitionTablePart.size)})`,
                    value: gptPartitionTablePart.size
                })
            }

            const mbrPartitionTablePart = get(newMedia, 'diskInfo.mbrPartitionTablePart');
            if (mbrPartitionTablePart) {
                newPartPathOptions.push({
                    title: `Master Boot Record (${formatBytes(mbrPartitionTablePart.size)})`,
                    value: mbrPartitionTablePart.size
                })
            }

            const rdbPartitionTablePart = get(newMedia, 'diskInfo.rdbPartitionTablePart');
            if (rdbPartitionTablePart) {
                newPartPathOptions.push({
                    title: `Rigid Disk Block (${formatBytes(rdbPartitionTablePart.size)})`,
                    value: rdbPartitionTablePart.size
                })
            }

            setOptimizePartPath(newPartPathOptions.length > 0 ? newPartPathOptions[0].value : null)
            setOptimizePartPathOptions(newPartPathOptions)
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
            size: (size * unitOption.size),
            byteswap
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
        setOptimizePartPath(null)
        setOptimizePartPathOptions([])
        setConnection(null)
    }

    const handleConfirm = async (confirmed) => {
        setOpenConfirm(false)
        if (!confirmed) {
            return
        }
        await handleOptimize()
    }

    const updateDisabled = isNil(path) || trim(path) === ''
    const optimizeDisabled = updateDisabled || isNil(media)
    
    return (
        <Box>
            <ConfirmDialog
                id="confirm-optimize"
                open={openConfirm}
                title="Optimize"
                description={`Do you want to optimize image file '${path}'${sizeFormatted}?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />
            <Title
                text="Optimize"
                description="Optimize image file size."
            />
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
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
                                id="optimize-browse-image-path"
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
            <Grid container spacing={0} direction="row" alignItems="center" sx={{ mt: 0 }}>
                <Grid item xs={12} lg={6}>
                    <Accordion title="Advanced" icon="gear" expanded={false} border={false}>
                        <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0.3}}>
                            <Grid item xs={12} lg={6}>
                                <SelectField
                                    label={
                                        <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                            <FontAwesomeIcon icon="location-crosshairs" style={{marginRight: '5px'}} /> Image size to optimize
                                        </div>
                                    }
                                    id="optimize-part-path"
                                    disabled={isNil(media)}
                                    emptyLabel="None available"
                                    value={optimizePartPath || ''}
                                    options={optimizePartPathOptions || []}
                                    onChange={(value) => {
                                        setOptimizePartPath(value)
                                        setSize(value === 'custom' ? get(media, 'diskSize') || 0 : value)
                                        setUnit('bytes')
                                    }}
                                />
                            </Grid>
                        </Grid>
                        <Grid container spacing={1} direction="row" sx={{mt: 1}}>
                            <Grid item xs={8} lg={4}>
                                <TextField
                                    label={
                                        <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                            <FontAwesomeIcon icon="ruler-horizontal" style={{marginRight: '5px'}} /> Size
                                        </div>
                                    }
                                    id="size"
                                    type="number"
                                    disabled={isNil(media)}
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
                                    disabled={isNil(media)}
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
                    </Accordion>
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0}}>
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
                                disabled={optimizeDisabled}
                                icon="compress"
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