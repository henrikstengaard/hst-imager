import { get, set } from "lodash"
import Title from "../components/Title"
import React from "react"
import Grid from "@mui/material/Grid"
import SelectField from "../components/SelectField"
import {AppStateContext} from "../components/AppStateContext"
import {AppStateDispatchContext} from "../components/AppStateContext"
import CheckboxField from "../components/CheckboxField";
import {ElectronIpc} from "../utils/ElectronIpc";
import Button from "../components/Button";

export default function Settings() {
    const electronIpc = new ElectronIpc()
    const appState = React.useContext(AppStateContext)
    const appStateDispatch = React.useContext(AppStateDispatchContext)

    const isMacOs = get(appState, 'isMacOs') || false
    const logsPath = get(appState, 'logsPath')
    const settings = get(appState, 'settings') || {}

    const openUrl = async (event, url) => {
        event.preventDefault()
        if (!url) {
            console.error('Url is null')
            return
        }
        if (!appState || !appState.isElectronActive)
        {
            console.error('Open url is only available with Electron')
            return
        }
        await electronIpc.openExternal({
            url
        })
    }
    
    const saveSettings = async ({ name, value } = {}) => {
        set(settings, name, value)
        
        const response = await fetch('api/settings', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({...settings})
        });
        if (!response.ok) {
            console.error('Failed to save settings')
        }

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
                        onChange={async (value) => await saveSettings({ name: 'macOsElevateMethod', value })}
                    />
                </Grid>
            </Grid>
            <Grid container spacing={1} direction="row" alignItems="center" sx={{mt: 1}}>
                <Grid item xs={12} lg={6}>
                    <CheckboxField
                        id="debug-mode"
                        label="Debug mode (applied after restart of app)"
                        value={debugMode}
                        onChange={async (checked) => await saveSettings({ name: 'debugMode', value: checked })}
                    />
                </Grid>
            </Grid>
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