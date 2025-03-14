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
import {HubConnectionBuilder} from "@microsoft/signalr";
import Typography from "@mui/material/Typography";
import CheckboxField from "../components/CheckboxField";
import SelectField from "../components/SelectField";
import {BackendApiStateContext} from "../components/BackendApiContext";
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
    const [openConfirm, setOpenConfirm] = React.useState(false);
    const [sourceMedia, setSourceMedia] = React.useState(null)
    const [medias, setMedias] = React.useState(null)
    const [byteswap, setByteswap] = React.useState(false);
    const [size, setSize] = React.useState(0)
    const [unit, setUnit] = React.useState('bytes')
    const [destinationPath, setDestinationPath] = React.useState(null)
    const [startOffset, setStartOffset] = React.useState(0);
    const [readPartPath, setReadPartPath] = React.useState(null)
    const [readPartPathOptions, setReadPartPathOptions] = React.useState([])
    const [connection, setConnection] = React.useState(null);
    const {
        backendBaseUrl,
        backendApi
    } = React.useContext(BackendApiStateContext)

    const readPartPathOption = readPartPathOptions.find(x => x.value === readPartPath)
    const formattedReadPartPath = readPartPathOption ? (readPartPath === 'custom'
        ? `- Start offset ${startOffset}`
        : ` ${readPartPathOption.title}`) : '';

    const unitOption = unitOptions.find(x => x.value === unit)
    const formattedSize = unitOption ? (readPartPath === 'custom'
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

    const getInfo = React.useCallback(async (path, byteswap) => {
        await backendApi.updateInfo({ path, sourceType: 'PhysicalDisk', byteswap });
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

            // default start offset, size and unit set to largest readable size
            setStartOffset(0)
            setSize(0)
            setUnit('bytes')

            // no media, reset
            if (isNil(media)) {
                setReadPartPath(null)
                setReadPartPathOptions([])
                return
            }

            // build new read part path options                        
            const newReadPartPathOptions = [{
                title: `Disk (${formatBytes(media.diskSize)})`,
                value: media.path
            }]

            const directoryPathSeparator = media.path.startsWith('/') ? '/' : '\\'
            
            const gptPartitionTablePart = get(media, 'diskInfo.gptPartitionTablePart');
            if (gptPartitionTablePart) {
                newReadPartPathOptions.push({
                    title: `Guid Partition Table (${formatBytes(gptPartitionTablePart.size)})`,
                    value: media.path + directoryPathSeparator + 'gpt'
                })

                gptPartitionTablePart.parts.filter(part => part.partType === 'Partition').forEach(part => {
                    const type = part.partitionType === part.fileSystem
                        ? part.partitionType
                        : `${part.partitionType}, ${part.fileSystem}`;

                    newReadPartPathOptions.push({
                        title: `- Partition #${part.partitionNumber}: ${type} (${formatBytes(part.size)})`,
                        value: media.path + directoryPathSeparator + 'gpt' + directoryPathSeparator + part.partitionNumber
                    })
                })
            }

            const mbrPartitionTablePart = get(media, 'diskInfo.mbrPartitionTablePart');
            if (mbrPartitionTablePart) {
                newReadPartPathOptions.push({
                    title: `Master Boot Record (${formatBytes(mbrPartitionTablePart.size)})`,
                    value: media.path + directoryPathSeparator + 'mbr'
                })

                mbrPartitionTablePart.parts.filter(part => part.partType === 'Partition').forEach(part => {
                    const type = part.partitionType === part.fileSystem
                        ? part.partitionType
                        : `${part.partitionType}, ${part.fileSystem}`;

                    newReadPartPathOptions.push({
                        title: `- Partition #${part.partitionNumber}: ${type} (${formatBytes(part.size)})`,
                        value: media.path + directoryPathSeparator + 'mbr' + directoryPathSeparator + part.partitionNumber
                    })
                })
            }

            const rdbPartitionTablePart = get(media, 'diskInfo.rdbPartitionTablePart');
            if (rdbPartitionTablePart) {
                newReadPartPathOptions.push({
                    title: `Rigid Disk Block (${formatBytes(rdbPartitionTablePart.size)})`,
                    value: media.path + directoryPathSeparator + 'rdb'
                })

                rdbPartitionTablePart.parts.filter(part => part.partType === 'Partition').forEach(part => {
                    const type = part.partitionType === part.fileSystem
                        ? part.partitionType
                        : `${part.partitionType}, ${part.fileSystem}`;

                    newReadPartPathOptions.push({
                        title: `- Partition #${part.partitionNumber}: ${type} (${formatBytes(part.size)})`,
                        value: media.path + directoryPathSeparator + 'rdb' + directoryPathSeparator + part.partitionNumber
                    })
                })
            }

            newReadPartPathOptions.push({
                title: 'Custom',
                value: 'custom'
            })
            
            setReadPartPath(newReadPartPathOptions.length > 0 ? newReadPartPathOptions[0].value : null)
            setReadPartPathOptions(newReadPartPathOptions)
        });

        newConnection.on('List', async (medias) => {
            const newMedia = getMedia({medias: medias, path: get(sourceMedia, 'path')});

            setMedias(medias || [])
            if (newMedia) {
                setSourceMedia(newMedia)
                await backendApi.updateInfo({ path: newMedia.path, byteswap })
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
    }, [backendApi, backendBaseUrl, byteswap, connection, getMedia, getMedias, setConnection, sourceMedia])
    
    const handleRead = async () => {
        await backendApi.startRead({
            title: `Reading disk '${sourceMedia.name}${formattedReadPartPath}' to file '${destinationPath}'${formattedSize}`,
            sourcePath: isNil(readPartPath) || readPartPath === 'custom' ? sourceMedia.path : readPartPath,
            destinationPath,
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
        await handleRead()
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
        setMedias(null)
        setByteswap(false)
        setSize(0)
        setUnit('bytes')
        setDestinationPath(null)
        setReadPartPath(null)
        setReadPartPathOptions([])
        setConnection(null)
    }
    
    const readDisabled = isNil(sourceMedia) || isNil(destinationPath)

    return (
        <Box>
            <ConfirmDialog
                id="confirm-read"
                open={openConfirm}
                title="Read"
                description={`Do you want to read disk '${sourceMedia === null ? '' : sourceMedia.name}${formattedReadPartPath}' to file '${destinationPath}'${formattedSize}?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />
            <Title
                text="Read"
                description="Reads disk or part of (partition or custom) from physical drive or image file to an image file."
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
                        onChange={async (media) => {
                            setSourceMedia(media)
                            await getInfo(media.path, byteswap)
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0}}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label="Part of disk to read"
                        id="read-part-path"
                        emptyLabel="None available"
                        value={readPartPath || ''}
                        options={readPartPathOptions || []}
                        onChange={(value) => {
                            setReadPartPath(value)
                            setStartOffset(0);
                            setSize(value === 'custom' ? get(sourceMedia, 'diskSize') || 0 : 0)
                            setUnit('bytes')
                        }}
                    />
                </Grid>
            </Grid>
            {readPartPath === 'custom' && (
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
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0}}>
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
                                fileFilters = {[{
                                    name: 'Hard disk image files',
                                    extensions: ['img', 'hdf', 'vhd', 'gz', 'zip']
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
                                await getInfo(sourceMedia.path, checked)
                            }
                        }}
                    />
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
                                icon="sync-alt"
                                onClick={async () => handleUpdate()}
                            >
                                Update
                            </Button>
                            <Button
                                disabled={readDisabled}
                                icon="upload"
                                onClick={async () => setOpenConfirm(true)}
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