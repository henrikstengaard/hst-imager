import React from 'react'
import {get, isNil, set} from "lodash";
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

const initialState = {
    confirmOpen: false,
    sourcePath: null,
    destinationPath: null
}

export default function Convert() {
    const [state, setState] = React.useState({...initialState})

    const {
        confirmOpen,
        sourcePath,
        destinationPath
    } = state

    const handleChange = ({name, value}) => {
        set(state, name, value)
        setState({...state})
    }

    const handleCancel = () => {
        setState({...initialState})
    }

    const handleConvert = async () => {
        const response = await fetch('api/convert', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                title: `Converting file '${sourcePath}' to file '${destinationPath}'`,
                sourcePath,
                destinationPath
            })
        });
        if (!response.ok) {
            console.error('Failed to convert')
        }
    }
    
    const handleConfirm = async (confirmed) => {
        setState({
            ...state,
            confirmOpen: false
        })
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
                open={confirmOpen}
                title="Convert"
                description={`Do you want to convert file '${sourcePath}' to file '${destinationPath}'?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />            
            <Title
                text="Convert"
                description="Convert image file from one format to another."
            />
            <Grid container spacing="2" direction="row" alignItems="center" sx={{mt: 2}}>
                <Grid item xs={12} lg={6}>
                    <TextField
                        id="source-path"
                        label={
                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                <FontAwesomeIcon icon="file" style={{marginRight: '5px'}} /> Source image file
                            </div>
                        }
                        value={sourcePath || ''}
                        endAdornment={
                            <BrowseOpenDialog
                                id="browse-source-path"
                                title="Select source image file"
                                onChange={(path) => handleChange({
                                    name: 'sourcePath',
                                    value: path
                                })}
                            />
                        }
                        onChange={(event) => handleChange({
                            name: 'sourcePath',
                            value: get(event, 'target.value'
                            )
                        })}
                    />
                </Grid>
            </Grid>
            <Grid container spacing="2" direction="row" alignItems="center" sx={{mt: 2}}>
                <Grid item xs={12} lg={6}>
                    <TextField
                        id="destination-path"
                        label={
                            <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                <FontAwesomeIcon icon="file" style={{marginRight: '5px'}} /> Destination image file
                            </div>
                        }
                        value={destinationPath || ''}
                        endAdornment={
                            <BrowseSaveDialog
                                id="browse-destination-path"
                                title="Select destination image file"
                                onChange={(path) => handleChange({
                                    name: 'destinationPath',
                                    value: path
                                })}
                            />
                        }
                        onChange={(event) => handleChange({
                            name: 'destinationPath',
                            value: get(event, 'target.value'
                            )
                        })}
                    />
                </Grid>
            </Grid>
            <Grid container spacing="2" direction="row" alignItems="center" sx={{mt: 2}}>
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
                                disabled={convertDisabled}
                                icon="exchange-alt"
                                onClick={async () => handleChange({
                                    name: 'confirmOpen',
                                    value: true
                                })}
                            >
                                Start convert
                            </Button>
                        </Stack>
                    </Box>
                </Grid>
            </Grid>
        </Box>
    )
}