import React from 'react'
import {get, isNil} from "lodash";
import Box from "@mui/material/Box";
import Title from "../components/Title";
import Grid from "@mui/material/Grid";
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import TextField from "../components/TextField";
import BrowseSaveDialog from "../components/BrowseSaveDialog";
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

export default function Convert() {
    const [openConfirm, setOpenConfirm] = React.useState(false)
    const [media, setMedia] = React.useState(null)
    const [sourcePath, setSourcePath] = React.useState(null)
    const [byteswap, setByteswap] = React.useState(false);
    const [destinationPath, setDestinationPath] = React.useState(null)
    const [connection, setConnection] = React.useState(null);
    const {
        backendBaseUrl,
        backendApi
    } = React.useContext(BackendApiStateContext)

    const handleCancel = () => {
        setOpenConfirm(false);
        setMedia(null)
        setSourcePath(null)
        setByteswap(false)
        setDestinationPath(null)
    }

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
        await backendApi.updateInfo({ path, byteswap });
    }, [backendApi])
    
    const handleConvert = async () => {
        await backendApi.startConvert({
            title: `Converting file '${sourcePath}' to file '${destinationPath}'`,
            sourcePath,
            destinationPath,
            byteswap
        });
    }
    
    const handleConfirm = async (confirmed) => {
        setOpenConfirm(false)
        if (!confirmed) {
            return
        }
        await handleConvert()
    }

    const convertDisabled = isNil(sourcePath) || isNil(destinationPath)

    return (
        <Box>
            <ConfirmDialog
                id="confirm-convert"
                open={openConfirm}
                title="Convert"
                description={`Do you want to convert file '${sourcePath}' to file '${destinationPath}'?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />            
            <Title
                text="Convert"
                description="Convert image file from one format to another."
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
                            if (media) {
                                setMedia(null)
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
                            <BrowseSaveDialog
                                id="browse-destination-path"
                                title="Select destination image file"
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
                    />
                </Grid>
            </Grid>
            <Grid container spacing={0} direction="row" alignItems="center" sx={{ mt: 0 }}>
                <Grid item xs={12} lg={6}>
                    <Accordion title="Advanced" icon="gear" expanded={false} border={false}>
                        <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0}}>
                            <Grid item xs={12}>
                                <CheckboxField
                                    id="byteswap"
                                    label="Byteswap source sectors"
                                    value={byteswap}
                                    onChange={async (checked) => {
                                        setByteswap(checked)
                                        if (media) {
                                            await getInfo(media.path, checked)
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
                                disabled={convertDisabled}
                                icon="exchange-alt"
                                onClick={async () => setOpenConfirm(true)}
                            >
                                Start convert
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