import React from 'react'
import Box from "@mui/material/Box";
import Title from "../components/Title";
import Grid from "@mui/material/Grid";
import TextField from "../components/TextField";
import {FontAwesomeIcon} from "@fortawesome/react-fontawesome";
import BrowseSaveDialog from "../components/BrowseSaveDialog";
import {get, isNil} from "lodash";
import Stack from "@mui/material/Stack";
import RedirectButton from "../components/RedirectButton";
import Button from "../components/Button";
import SelectField from "../components/SelectField";
import CheckboxField from "../components/CheckboxField";
import ConfirmDialog from "../components/ConfirmDialog";
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

export default function Blank() {
    const [openConfirm, setOpenConfirm] = React.useState(false)
    const [path, setPath] = React.useState(null)
    const [size, setSize] = React.useState(16)
    const [unit, setUnit] = React.useState('gb')
    const [compatibleSize, setCompatibleSize] = React.useState(true)
    const {
        backendApi
    } = React.useContext(BackendApiStateContext)

    const handleBlank = async () => {
        const unitOption = unitOptions.find(x => x.value === unit)
        await backendApi.startBlank({
            title: `Creating ${size} ${unitOption.title} blank image '${path}'`,
            path,
            size: (size * unitOption.size),
            compatibleSize
        })
    }
    
    const handleCancel = () => {
        setOpenConfirm(false);
        setPath(null);
        setSize(16);
        setUnit('gb');
        setCompatibleSize(true);
    }

    const handleConfirm = async (confirmed) => {
        setOpenConfirm(false)
        if (!confirmed) {
            return
        }
        await handleBlank()
    }
    
    const blankDisabled = isNil(path) || size <= 0
    
    return (
        <Box>
            <ConfirmDialog
                id="confirm-blank"
                open={openConfirm}
                title="Blank"
                description={`Do you want to create blank image file file '${path}' with size '${size} ${unit.toUpperCase()}'?`}
                onClose={async (confirmed) => await handleConfirm(confirmed)}
            />
            <Title
                text="Blank"
                description="Create a blank image file."
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
                            <BrowseSaveDialog
                                id="blank-image-path"
                                title="Select image file to create"
                                onChange={(path) => setPath(path)}
                            />
                        }
                        onChange={(event) => setPath(get(event, 'target.value'))}
                        onKeyDown={async (event) => {
                            if (event.key !== 'Enter') {
                                return
                            }
                            await handleBlank()
                        }}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={0} direction="row" alignItems="center" sx={{ mt: 0 }}>
                <Grid item xs={12} lg={6}>
                    <Grid container spacing={1} direction="row" sx={{mt: 0.3}}>
                        <Grid item xs={8} lg={4}>
                            <TextField
                                label={
                                    <div style={{display: 'flex', alignItems: 'center', verticalAlign: 'bottom'}}>
                                        <FontAwesomeIcon icon="ruler-horizontal" style={{marginRight: '5px'}} /> Size
                                    </div>
                                }
                                id="size"
                                type="number"
                                value={size}
                                inputProps={{min: 0, style: { textAlign: 'right' }}}
                                onChange={(event) => setSize(event.target.value)}
                                onKeyDown={async (event) => {
                                    if (event.key !== 'Enter') {
                                        return
                                    }
                                    await handleBlank()
                                }}
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
                                value={unit || ''}
                                options={unitOptions}
                                onChange={(value) => setUnit(value)}
                            />
                        </Grid>
                    </Grid>
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0}}>
                <Grid item xs={12}>
                    <CheckboxField
                        id="compatible-size"
                        label="Size compatible with various SD/CF-cards, SSD and hard-disk brands"
                        value={compatibleSize}
                        onChange={(checked) => setCompatibleSize(checked)}
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
                                disabled={blankDisabled}
                                icon="plus"
                                onClick={async () => setOpenConfirm(true)}
                            >
                                Create blank image
                            </Button>
                        </Stack>
                    </Box>
                </Grid>
            </Grid>
        </Box>
    )
}