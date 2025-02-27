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

export default function Settings() {
    const appState = React.useContext(AppStateContext)
    const host = get(appState, 'host') || 'default'
    
    const appStateDispatch = React.useContext(AppStateDispatchContext)
    const {
        backendApi
    } = React.useContext(BackendApiStateContext)

    const isMacOs = get(appState, 'isMacOs') || false
    const logsPath = get(appState, 'logsPath')
    const settings = get(appState, 'settings') || {}

    const openUrl = async (event, url) => {
        event.preventDefault()
        await appState.hostIpc.openExternal({ url })
    }
    
    const saveSettings = async ({ name, value } = {}) => {
        set(settings, name, value)
        
        await backendApi.updateSettings({...settings});

        appStateDispatch({
            type: 'updateAppState',
            appState: {
                ...appState,
                settings: {...settings}
            }
        })
    }
    
    const macOsElevateMethodOptions = [{
        title: 'Osascript sudo',
        value: 'OsascriptSudo'
    },{
        title: 'Osascript administrator privileges',
        value: 'OsascriptAdministrator'
    }]
    
    const {
        macOsElevateMethod = 'OsascriptSudo',
        debugMode
    } = settings
    
    const isDebugModeDisabled = isMacOs && macOsElevateMethod === 'OsascriptAdministrator';

    return (
        <React.Fragment>
            <Title
                text="Settings"
            />
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12} lg={6}>
                    <SelectField
                        label="macOS elevate method"
                        id="macos-elevate-method"
                        disabled={!isMacOs}
                        value={macOsElevateMethod || ''}
                        options={macOsElevateMethodOptions}
                        onChange={async (value) => {
                            if (value === 'OsascriptAdministrator') {
                                set(settings, 'debugMode', false);
                            }
                            await saveSettings({ name: 'macOsElevateMethod', value })
                        }}
                    />
                </Grid>
            </Grid>
            {host !== 'neo' && (
                <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                    <Grid item xs={12} lg={6}>
                        <CheckboxField
                            id="debug-mode"
                            label="Debug mode (applied after restart of app)"
                            disabled={isDebugModeDisabled}
                            value={debugMode}
                            onChange={async (checked) => await saveSettings({ name: 'debugMode', value: checked })}
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