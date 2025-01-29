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
    title: "PFS\\3",
    value: "pfs3"
}];

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
    const [formatAll, setFormatAll] = React.useState(true);
    const [prefillSize, setPrefillSize] = React.useState(null)
    const [prefillSizeOptions, setPrefillSizeOptions] = React.useState([])
    const [connection, setConnection] = React.useState(null);
    const {
        backendBaseUrl,
        backendApi
    } = React.useContext(BackendApiStateContext)

    const unitOption = unitOptions.find(x => x.value === unit)
    const sizeFormatted = size === 0 ? '' : `, size ${size} ${unitOption.title}`
    const sourceTypeFormatted = sourceType === 'ImageFile' ? 'image file' : 'disk'
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
            setMedia(newMedia)

            // no media, reset
            if (isNil(newMedia)) {
                setPrefillSize(null)
                setPrefillSizeOptions([])
                return
            }

            // add select prefill option with disk size                       
            const newPrefillSizeOptions = [{
                title: 'Select size to prefill',
                value: 'prefill'
            }, {
                title: `Disk (${newMedia.diskSize} bytes)`,
                value: newMedia.diskSize
            }]

            setPrefillSize(newPrefillSizeOptions.length > 0 ? 'prefill' : null)
            setPrefillSizeOptions(newPrefillSizeOptions)
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
            size: (size * unitOption.size),
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
        setPrefillSize(null)
        setPrefillSizeOptions([])
        setConnection(null)
    }

    const handleUpdate = async () => {
        await backendApi.updateList()
    }

    const formatDisabled = isNil(path)

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
                description="Format an image file or physical disk."
            />
            <Grid container spacing={1} direction="row" alignItems="center" sx={{ mt: 1 }}>
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
            <Grid container spacing={1} direction="row" alignItems="center" sx={{ mt: 1 }}>
                <Grid item xs={12} lg={6}>
                    {sourceType === 'ImageFile' && (
                        <TextField
                            id="image-path"
                            label={
                                <div style={{ display: 'flex', alignItems: 'center', verticalAlign: 'bottom' }}>
                                    <FontAwesomeIcon icon="file" style={{ marginRight: '5px' }} /> Source file
                                </div>
                            }
                            value={path || ''}
                            endAdornment={
                                <BrowseOpenDialog
                                    id="browse-image-path"
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
                                }
                            }}
                            onKeyDown={async (event) => {
                                if (event.key !== 'Enter') {
                                    return
                                }
                                await getInfo(path, sourceType, byteswap)
                            }}
                        />
                    )}
                    {sourceType === 'PhysicalDisk' && (
                        <MediaSelectField
                            id="source-media-path"
                            label={
                                <div style={{ display: 'flex', alignItems: 'center', verticalAlign: 'bottom' }}>
                                    <FontAwesomeIcon icon="hdd" style={{ marginRight: '5px' }} /> Source disk
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
                    )}
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{ mt: 1 }}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label="Format type"
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
                                    break;
                                default:
                                    setFileSystemOptions([])
                            }
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{ mt: 1 }}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label="File system"
                        id="file-system"
                        emptyLabel="None available"
                        value={fileSystem || ''}
                        options={fileSystemOptions || []}
                        onChange={(value) => {
                            setFileSystem(value)
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{ mt: 1 }}>
                <Grid item xs={12}>
                    <CheckboxField
                        id="byteswap"
                        label="Byteswap source sectors"
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
            <Grid container spacing={1} direction="row" alignItems="center" sx={{ mt: 1 }}>
                <Grid item xs={12}>
                    <CheckboxField
                        id="format-all"
                        label="Format entire image/disk"
                        value={formatAll}
                        onChange={(checked) => {
                            setSize(checked ? 0 : get(media, 'diskSize') || 0)
                            setUnit('bytes')
                            setFormatAll(checked)
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{ mt: 1 }}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label="Prefill size to format"
                        id="prefill-size"
                        emptyLabel="None available"
                        disabled={formatAll}
                        value={prefillSize || ''}
                        options={prefillSizeOptions || []}
                        onChange={(value) => {
                            setSize(value)
                            setUnit('bytes')
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" sx={{ mt: 1 }}>
                <Grid item xs={8} lg={4}>
                    <TextField
                        label="Size"
                        id="size"
                        type={formatAll ? "text" : "number"}
                        disabled={formatAll}
                        value={formatAll ? '' : size}
                        inputProps={{ min: 0, style: { textAlign: 'right' } }}
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
                        disabled={formatAll}
                        value={unit || ''}
                        options={unitOptions}
                        onChange={(value) => setUnit(value)}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{ mt: 1 }}>
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
                                icon="sync-alt"
                                onClick={async () => handleUpdate()}
                            >
                                Update
                            </Button>
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