import { get, set } from "lodash"
import Title from "../components/Title"
import React from "react"
import Grid from "@mui/material/Grid"
import SelectField from "../components/SelectField"
import {AppStateContext} from "../components/AppStateContext"
import {AppStateDispatchContext} from "../components/AppStateContext"
import CheckboxField from "../components/CheckboxField";
import Button from "../components/Button";
import {BackendApiStateContext} from "../components/BackendApiContext";
import TextField from "../components/TextField";
import useDebounce from "../hooks/UseDebounce";
import Box from "@mui/material/Box";
import {Alert} from "@mui/material";

export default function Settings() {
    const appState = React.useContext(AppStateContext)
    const host = get(appState, 'host') || 'default'
    
    const appStateDispatch = React.useContext(AppStateDispatchContext)
    const {
        backendApi
    } = React.useContext(BackendApiStateContext)

    const settings= get(appState, 'settings') || {};

    const [allPhysicalDrives, setAllPhysicalDrives] = React.useState(get(settings, 'allPhysicalDrives') || false);
    const [macOsElevateMethod, setMacOsElevateMethod] = React.useState(get(settings, 'macOsElevateMethod') || 'OsascriptAdministrator');
    const [retries, setRetries] = React.useState(get(settings, 'retries') || 5);
    const [force, setForce] = React.useState(get(settings, 'force') || false);
    const [verify, setVerify] = React.useState(get(settings, 'verify') || false);
    const [skipUnusedSectors, setSkipUnusedSectors] = React.useState(get(settings, 'verify') || true);
    const [debugMode, setDebugMode] = React.useState(get(settings, 'debugMode') || false);

    const isMacOs = get(appState, 'isMacOs') || false
    const logsPath = get(appState, 'logsPath')

    const openUrl = async (event, url) => {
        event.preventDefault()
        await appState.hostIpc.openExternal({ url })
    }

    const updateSettings = useDebounce(async () => {
        await backendApi.updateSettings({...settings});

        appStateDispatch({
            type: 'updateAppState',
            appState: {
                ...appState,
                settings: {...settings}
            }
        })
    }, 1000);

    const handleChange = async ({ name, value } = {}) => {
        set(settings, name, value);
        updateSettings();
    }
    
    const macOsElevateMethodOptions = [{
        title: 'Osascript sudo',
        value: 'OsascriptSudo'
    },{
        title: 'Osascript administrator privileges',
        value: 'OsascriptAdministrator'
    }]
    
    const isDebugModeDisabled = isMacOs && macOsElevateMethod === 'OsascriptAdministrator';

    return (
        <React.Fragment>
            <Title
                text="Settings"
                description="Configure settings for read, write, and debug."
            />
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0}}>
                <Grid item xs={12} lg={6}>
                    <CheckboxField
                        id="allPhysicalDrives"
                        label="Show all physical drives (except system/boot drives)"
                        value={allPhysicalDrives}
                        onChange={async (checked) => {
                            setAllPhysicalDrives(checked);
                            await handleChange({ name: 'allPhysicalDrives', value: checked });
                        }}
                    />
                    <Alert severity="warning" sx={{ mt: 1 }}>
                        All physical drive shown enables Hst Imager to read and write all disks except system and boot drives. Be very sure to select the correct physical drive when this is enabled otherwise Hst Imager might destroy your disk and it's file system!
                    </Alert>
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label="macOS elevate method"
                        id="macos-elevate-method"
                        disabled={!isMacOs}
                        value={macOsElevateMethod}
                        options={macOsElevateMethodOptions}
                        onChange={async (value) => {
                            if (value === 'OsascriptAdministrator') {
                                setDebugMode(false);
                                set(settings, 'debugMode', false);
                            }
                            setMacOsElevateMethod(value);
                            await handleChange({ name: 'macOsElevateMethod', value })
                        }}
                    />
                </Grid>
            </Grid>
            <Box sx={{mt: 4}}>
                <Title
                    text="Read and write"
                />
            </Box>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 0}}>
                <Grid item xs={2} lg={2}>
                    <TextField
                        label="Retries"
                        id="retries"
                        type="number"
                        value={retries}
                        inputProps={{min: 0, style: { textAlign: 'right' }}}
                        onChange={async (event) => {
                            setRetries(event.target.value);
                            await handleChange({ name: 'retries', value: event.target.value });
                        }}
                    />
                </Grid>
                <Grid item xs={12}>
                    <CheckboxField
                        id="force"
                        label="Force and ignore errors when retries are exceeded"
                        value={force}
                        onChange={async (checked) => {
                            setForce(checked);
                            await handleChange({ name: 'force', value: checked })
                        }}
                    />
                </Grid>
                <Grid item xs={12}>
                    <CheckboxField
                        id="verify"
                        label="Verify while reading and writing"
                        value={verify}
                        onChange={async (checked) => {
                            setVerify(checked);
                            await handleChange({ name: 'verify', value: checked });
                        }}
                    />
                </Grid>
                <Grid item xs={12}>
                    <CheckboxField
                        id="skip-unused-sectors"
                        label="Skip unused sectors"
                        value={skipUnusedSectors}
                        onChange={async (checked) => {
                            setSkipUnusedSectors(checked);
                            await handleChange({ name: 'skipUnusedSectors', value: checked })
                        }}
                    />
                </Grid>
            </Grid>
            <Box sx={{mt: 4}}>
                <Title
                    text="Debug"
                />
            </Box>
            {host !== 'neo' && (
                <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                    <Grid item xs={12} lg={6}>
                        <CheckboxField
                            id="debug-mode"
                            label="Debug mode (applied after restart of app)"
                            disabled={isDebugModeDisabled}
                            value={debugMode}
                            onChange={async (checked) => {
                                setDebugMode(checked);
                                await handleChange({ name: 'debugMode', value: checked })
                            }}
                        />
                    </Grid>
                </Grid>
            )}
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12} lg={6}>
                    <Button
                        icon="chart-line"
                        onClick={async (event) => openUrl(event, logsPath)}
                    >
                        View logs
                    </Button>
                </Grid>
            </Grid>
        </React.Fragment>
    )
}