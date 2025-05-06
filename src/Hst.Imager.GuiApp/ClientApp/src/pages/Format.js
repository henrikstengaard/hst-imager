import React from 'react'
import Box from "@mui/material/Box";
import Title from "../components/Title";
import Grid from "@mui/material/Grid";
import TextField from "../components/TextField";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import BrowseOpenDialog from "../components/BrowseOpenDialog";
import { get, isNil } from "lodash";
import MediaSelectField from "../components/MediaSelectField";
import Stack from "@mui/material/Stack";
import RedirectButton from "../components/RedirectButton";
import Button from "../components/Button";
import ConfirmDialog from "../components/ConfirmDialog";
import { HubConnectionBuilder } from "@microsoft/signalr";
import Typography from "@mui/material/Typography";
import Media from "../components/Media";
import CheckboxField from "../components/CheckboxField";
import SelectField from "../components/SelectField";
import { BackendApiStateContext } from "../components/BackendApiContext";
import IconButton from "@mui/material/IconButton";
import Accordion from "../components/Accordion";

const unitOptions = [{
    title: 'GB',
    value: 'gb',
    size: Math.pow(10, 9)
}, {
    title: 'MB',
    value: 'mb',
    size: Math.pow(10, 6)
}, {
    title: 'KB',
    value: 'kb',
    size: Math.pow(10, 3)
}, {
    title: 'Bytes',
    value: 'bytes',
    size: 1
}];

const typeOptions = [{
    title: 'Image file',
    value: 'ImageFile'
}, {
    title: 'Physical disk',
    value: 'PhysicalDisk'
}]

const formatTypeOptions = [{
    title: "Master Boot Record",
    value: "mbr"
}, {
    title: "Guid Partition Table",
    value: "gpt"
}, {
    title: "Rigid Disk Block",
    value: "rdb"
}, {
    title: "PiStorm",
    value: "pistorm"
}];

const basicFileSystemOptions = [{
    title: "FAT32",
    value: "fat32"
}, {
    title: "exFAT",
    value: "exfat"
}, {
    title: "NTFS",
    value: "ntfs"
}];

const rdbFileSystemOptions = [{
    title: "PDS\\3 (direct scsi)",
    value: "pds3"
}, {
    title: "PFS\\3",
    value: "pfs3"
}, {
    title: "DOS\\3",
    value: "dos3"
}, {
    title: "DOS\\7 (long filename)",
    value: "dos7"
}];

const maxPartitionSizeOptions = [{
    title: "128 GB",
    value: "137438953472" // math.pow(2, 37)
}, {
    title: "64 GB",
    value: "68719476736" // math.pow(2, 36)
}, {
    title: "32 GB",
    value: "34359738368" // math.pow(2, 35)
}, {
    title: "16 GB",
    value: "17179869184" // math.pow(2, 34)
}, {
    title: "8 GB",
    value: "8589934592" // math.pow(2, 33)
}, {
    title: "4 GB",
    value: "4294967296" // math.pow(2, 32)
}, {
    title: "2 GB",
    value: "2147483648" // math.pow(2, 31)
}, {
    title: "1 GB",
    value: "1073741824" // math.pow(2, 30)
}];

const pfs3AioUrl = 'https://aminet.net/disk/misc/pfs3aio.lha';

export default function Format() {
    const [openConfirm, setOpenConfirm] = React.useState(false);
    const [media, setMedia] = React.useState(null)
    const [path, setPath] = React.useState(null)
    const [byteswap, setByteswap] = React.useState(false);
    const [medias, setMedias] = React.useState(null)
    const [size, setSize] = React.useState(0)
    const [unit, setUnit] = React.useState('bytes')
    const [sourceType, setSourceType] = React.useState('ImageFile')
    const [formatType, setFormatType] = React.useState('mbr')
    const [fileSystem, setFileSystem] = React.useState('fat32')
    const [fileSystemOptions, setFileSystemOptions] = React.useState(basicFileSystemOptions)
    const [maxPartitionSize, setMaxPartitionSize] = React.useState(maxPartitionSizeOptions[0].value)
    const [downloadPfs3Aio, setDownloadPfs3Aio] = React.useState(true);
    const [fileSystemPath, setFileSystemPath] = React.useState(pfs3AioUrl);
    const [connection, setConnection] = React.useState(null);
    const {
        backendBaseUrl,
        backendApi
    } = React.useContext(BackendApiStateContext)

    const unitOption = unitOptions.find(x => x.value === unit)
    const sizeFormatted = size === 0 ? '' : `, size ${size} ${unitOption.title}`
    const sourceTypeFormatted = sourceType === 'ImageFile' ? 'image file' : 'physical disk'
    const formatTypeOption = formatTypeOptions.find(x => x.value === formatType)
    const fileSystemOption = fileSystemOptions.find(x => x.value === fileSystem)

    const getMedia = React.useCallback(({ medias, path }) => {
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

        newConnection.on("Info", (newMedia) => {
            setMedia(newMedia);
            setUnit('bytes');
            setSize(isNil(newMedia) ? 0 : newMedia.diskSize);
        });

        newConnection.on('List', async (medias) => {
            const newMedia = getMedia({ medias: medias, path: path });
            const newPath = get(newMedia, 'path');

            setMedias(medias || [])
            if (newMedia) {
                setPath(newPath)
                setMedia(newMedia)
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
    }, [backendBaseUrl, byteswap, connection, getInfo, getMedia, setConnection, path, sourceType])

    const handleSourceTypeChange = async (value) => {
        setSourceType(value)
        switch (value) {
            case 'PhysicalDisk':
                if (medias === null) {
                    setPath(null)
                    setMedia(null)
                    await getMedias()
                    return
                }

                const newMedia = getMedia({ medias: medias, path: path });
                const newPath = get(newMedia, 'path');

                setPath(newPath)
                setMedia(newMedia)
                if (value === 'PhysicalDisk' && newMedia) {
                    await getInfo(newPath, sourceType, byteswap)
                }

                break
            default:
                setPath(null)
                setMedia(null)
                break
        }
    }

    const handleFormat = async () => {
        await backendApi.startFormat({
            title: `Formatting ${sourceTypeFormatted} '${isNil(media) ? path : media.name}' with '${formatTypeOption.title}' format type, '${fileSystemOption.title}' file system${sizeFormatted}`,
            path,
            formatType,
            fileSystem,
            fileSystemPath,
            size: (size * unitOption.size),
            maxPartitionSize,
            byteswap
        });
    }

    const handleConfirm = async (confirmed) => {
        setOpenConfirm(false)
        if (!confirmed) {
            return
        }
        await handleFormat()
    }

    const handleCancel = () => {
        if (connection) {
            connection.stop()
        }
        setOpenConfirm(false)
        setMedia(null)
        setPath(null)
        setByteswap(false)
        setMedias(null)
        setSize(0)
        setUnit('bytes')
        setSourceType('ImageFile')
        setFormatType('mbr')
        setFileSystem('fat32')
        setConnection(null)
        setDownloadPfs3Aio(true)
        setFileSystemPath(pfs3AioUrl)
    }

    const handleUpdate = async () => {
        await backendApi.updateList()
    }

    const pathValid = !isNil(path) && path.trim().length > 0
    const fileSystemPathValid = formatType === 'mbr' || formatType === 'gpt' ||
        (!isNil(fileSystemPath) && fileSystemPath.trim().length > 0)
    
    const formatDisabled = !pathValid || !fileSystemPathValid

    return (
        <Box>
            <ConfirmDialog
                id="confirm-format"
                open={openConfirm}
                title="Format"
                description={`Do you want to format ${sourceTypeFormatted} '${isNil(media) ? path : media.name}' with '${formatTypeOption.title}' format type, '${fileSystemOption.title}' file system${sizeFormatted}?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />
            <Title
                text="Format"
                description="Format an image file or a physical disk."
            />
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
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
                        onChange={(value) => handleSourceTypeChange(value)}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{ mt: 0.3 }}>
                {sourceType === 'ImageFile' && (
                    <Grid item xs={12} lg={6}>
                        <TextField
                            id="image-path"
                            label={
                                <div style={{ display: 'flex', alignItems: 'center', verticalAlign: 'bottom' }}>
                                    <FontAwesomeIcon icon="file" style={{ marginRight: '5px' }} /> Source image file
                                </div>
                            }
                            value={path || ''}
                            endAdornment={
                                <BrowseOpenDialog
                                    id="format-image-path"
                                    title="Select image file"
                                    onChange={async (path) => {
                                        setPath(path)
                                        await getInfo(path, sourceType, byteswap)
                                    }}
                                    fileFilters={[{
                                        name: 'Hard disk image files',
                                        extensions: ['img', 'hdf', 'vhd']
                                    }, {
                                        name: 'All files',
                                        extensions: ['*']
                                    }]}
                                />
                            }
                            onChange={(event) => {
                                setPath(get(event, 'target.value'))
                                if (media) {
                                    setMedia(null)
                                    setSize(0);
                                    setUnit('bytes');
                                }
                            }}
                            onKeyDown={async (event) => {
                                if (event.key !== 'Enter') {
                                    return
                                }
                                await getInfo(path, sourceType, byteswap)
                            }}
                        />
                    </Grid>
                )}
                {sourceType === 'PhysicalDisk' && (
                    <Grid item xs={12} lg={6}>
                        <Stack direction="row">
                            <MediaSelectField
                                id="source-media-path"
                                label={
                                    <div style={{ display: 'flex', alignItems: 'center', verticalAlign: 'bottom' }}>
                                        <FontAwesomeIcon icon="hdd" style={{ marginRight: '5px' }} /> Source physical disk
                                    </div>
                                }
                                medias={medias || []}
                                path={path || ''}
                                onChange={async (media) => {
                                    setPath(media)
                                    setMedia(media.path)
                                    await getInfo(media.path, sourceType, byteswap)
                                }}
                            />
                            <IconButton
                                aria-label="refresh"
                                color="primary"
                                disableFocusRipple={true}
                                onClick={async () => handleUpdate()}
                            >
                                <FontAwesomeIcon icon="sync-alt" />
                            </IconButton>
                        </Stack>
                    </Grid>
                )}
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{ mt: 0.3 }}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label={
                            <div style={{ display: 'flex', alignItems: 'center', verticalAlign: 'bottom' }}>
                                <FontAwesomeIcon icon="sliders" style={{ marginRight: '5px' }} /> Format type
                            </div>
                        }
                        id="format-type"
                        emptyLabel="None available"
                        value={formatType || ''}
                        options={formatTypeOptions || []}
                        onChange={(value) => {
                            setFormatType(value)

                            switch (value) {
                                case 'mbr':
                                case 'gpt':
                                    setFileSystemOptions(basicFileSystemOptions)
                                    setFileSystem(basicFileSystemOptions[0].value)
                                    break;
                                case 'rdb':
                                case 'pistorm':
                                    setFileSystemOptions(rdbFileSystemOptions)
                                    setFileSystem(rdbFileSystemOptions[0].value)
                                    setDownloadPfs3Aio(true)
                                    setFileSystemPath(pfs3AioUrl)
                                    break;
                                default:
                                    setFileSystemOptions([])
                            }
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{ mt: 0.3 }}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label={
                            <div style={{ display: 'flex', alignItems: 'center', verticalAlign: 'bottom' }}>
                                <FontAwesomeIcon icon="sliders" style={{ marginRight: '5px' }} /> File system
                            </div>
                        }
                        id="file-system"
                        emptyLabel="None available"
                        value={fileSystem || ''}
                        options={fileSystemOptions || []}
                        onChange={(value) => {
                            setFileSystem(value)
                            const isPfs3FileSystem = value === 'pds3' || value === 'pfs3'
                            setDownloadPfs3Aio(isPfs3FileSystem)
                            setFileSystemPath(isPfs3FileSystem ? pfs3AioUrl : null)
                        }}
                    />
                </Grid>
            </Grid>
            {(formatType === 'rdb' || formatType === 'pistorm') && (
                <React.Fragment>
                    {(fileSystem === 'pds3' || fileSystem === 'pfs3') && (
                        <Grid container spacing={0} direction="row" sx={{ mt: 0 }}>
                            <Grid item xs={12} lg={6}>
                                <CheckboxField
                                    id="download-pfs3aio"
                                    label="Download pfs3aio from aminet.net"
                                    value={downloadPfs3Aio}
                                    onChange={async (checked) => {
                                        setDownloadPfs3Aio(checked)
                                        setFileSystemPath(checked ? pfs3AioUrl : null)
                                    }}
                                />
                            </Grid>
                        </Grid>
                    )}
                    {(!downloadPfs3Aio && (fileSystem === 'pds3' || fileSystem === 'pfs3' || fileSystem === 'dos3' || fileSystem === 'dos7')) && (
                        <Grid container spacing={1} direction="row" alignItems="center" sx={{ mt: 0.3 }}>
                            <Grid item xs={12} lg={6}>
                                <TextField
                                    id="file-system-path"
                                    label={
                                        <div style={{ display: 'flex', alignItems: 'center', verticalAlign: 'bottom' }}>
                                            <FontAwesomeIcon icon="file" style={{ marginRight: '5px' }} /> File system file
                                        </div>
                                    }
                                    value={fileSystemPath || ''}
                                    endAdornment={
                                        <BrowseOpenDialog
                                            id="file-system-file-path"
                                            title="Select file system file"
                                            onChange={async (path) => {
                                                setFileSystemPath(path)
                                            }}
                                            fileFilters={[{
                                                name: 'All files',
                                                extensions: ['*']
                                            }]}
                                        />
                                    }
                                    onChange={(event) => {
                                        setFileSystemPath(get(event, 'target.value'))
                                    }}
                                />
                            </Grid>
                        </Grid>
                    )}
                </React.Fragment>
            )}
            <Grid container spacing={0} direction="row" alignItems="center" sx={{ mt: 0 }}>
                <Grid item xs={12} lg={6}>
                    <Accordion title="Advanced" icon="gear" expanded={false} border={false}>
                        <Grid container spacing={1} direction="row" sx={{mt: 0.3}}>
                            <Grid item xs={12} lg={4}>
                                <TextField
                                    label={
                                        <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                            <FontAwesomeIcon icon="ruler-horizontal" style={{marginRight: '5px'}} /> Size
                                        </div>
                                    }
                                    disabled={isNil(media)}
                                    id="size"
                                    type={"number"}
                                    value={size}
                                    inputProps={{min: 0, style: { textAlign: 'right' }}}
                                    onChange={(event) => setSize(event.target.value)}
                                />
                            </Grid>
                            <Grid item xs={12} lg={4}>
                                <Stack direction="row">
                                    <SelectField
                                        label={
                                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                                <FontAwesomeIcon icon="scale-balanced" style={{marginRight: '5px'}} /> Unit
                                            </div>
                                        }
                                        disabled={isNil(media)}
                                        id="unit"
                                        value={unit || ''}
                                        options={unitOptions}
                                        onChange={(value) => setUnit(value)}
                                    />
                                    <IconButton
                                        aria-label="reset"
                                        color="primary"
                                        disabled={isNil(media)}
                                        disableFocusRipple={true}
                                        onClick={async () => {
                                            setSize(isNil(media) ? 0 : media.diskSize);
                                            setUnit('bytes');
                                        }}
                                    >
                                        <FontAwesomeIcon icon="rotate-left" />
                                    </IconButton>
                                </Stack>
                            </Grid>
                        </Grid>
                        {(formatType === 'rdb' || formatType === 'pistorm') && (
                            <Grid container spacing={1} direction="row" alignItems="center" sx={{ mt: 0.3 }}>
                                <Grid item xs={12} lg={12}>
                                    <SelectField
                                        label="Max partition size"
                                        id="max-partition-size"
                                        emptyLabel="None available"
                                        value={maxPartitionSize || ''}
                                        options={maxPartitionSizeOptions || []}
                                        onChange={(value) => {
                                            setMaxPartitionSize(value);
                                        }}
                                    />
                                </Grid>
                            </Grid>
                        )}
                        <Grid container spacing={0} direction="row" alignItems="center" sx={{ mt: 0 }}>
                            <Grid item xs={12}>
                                <CheckboxField
                                    id="byteswap"
                                    label="Byteswap sectors"
                                    value={byteswap}
                                    onChange={async (checked) => {
                                        setByteswap(checked)
                                        if (media) {
                                            await getInfo(media.path, sourceType, checked)
                                        }
                                    }}
                                />
                            </Grid>
                        </Grid>
                    </Accordion>
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{ mt: 0 }}>
                <Grid item xs={12} lg={6}>
                    <Box display="flex" justifyContent="flex-end">
                        <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                            <RedirectButton
                                path="/"
                                icon="ban"
                                onClick={async () => handleCancel()}
                            >
                                Cancel
                            </RedirectButton>
                            <Button
                                disabled={formatDisabled}
                                icon="check"
                                onClick={async () => setOpenConfirm(true)}
                            >
                                Format
                            </Button>
                        </Stack>
                    </Box>
                </Grid>
            </Grid>
            {media && media.diskInfo && (
                <Grid container spacing={1} direction="row" alignItems="center" sx={{ mt: 1 }}>
                    <Grid item xs={12}>
                        <Typography variant="h3">
                            Source {sourceTypeFormatted}
                        </Typography>
                        <Typography>
                            Disk information read from source {sourceTypeFormatted}.
                        </Typography>
                        <Media media={media} />
                    </Grid>
                </Grid>
            )}
        </Box>
    )
}