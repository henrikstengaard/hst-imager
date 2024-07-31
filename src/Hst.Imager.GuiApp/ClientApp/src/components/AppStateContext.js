import React from 'react'
import {BackendApiStateContext} from "./BackendApiContext";
import {NeutralinoHost} from "../hosts/NeutralinoHost";
import {ElectronHost} from "../hosts/ElectronHost";
import {NativeHost} from "../hosts/NativeHost";

export const AppStateContext = React.createContext(null)
export const AppStateDispatchContext = React.createContext(null)

function appStateReducer(state, action) {
    switch (action.type) {
        case 'updateAppState': {
            return {
                ...state,
                ...action.appState
            }
        }
        default: {
            throw new Error(`Unhandled action type: ${action.type}`)
        }
    }
}

function resolveHostIpc({ os, host, backendBaseUrl }) {
    switch(host) {
        case 'native':
            return new NativeHost({ backendBaseUrl });
        case 'electron':
            return new ElectronHost({ backendBaseUrl });
        case 'neutralinojs':
            return new NeutralinoHost({ os, backendBaseUrl });
        default:
            return null;
    }  
}

export function AppStateProvider(props) {
    const {
        os,
        host,
        children
    } = props

    const {
        backendBaseUrl,
        backendApi
    } = React.useContext(BackendApiStateContext)

    const hostIpc = resolveHostIpc({ os, host, backendBaseUrl })
    
    const [state, dispatch] = React.useReducer(appStateReducer, {}, () => null)

    const handleAppState = React.useCallback(() => {
        async function fetchAppState() {
            const appState = await backendApi.getAppState();

            const hasTitleBar = host !== 'neutralinojs';
            const hasControlButtons = host !== 'native';
            
            dispatch({
                type: 'updateAppState',
                appState: {
                    host,
                    hostIpc,
                    hasTitleBar,
                    hasControlButtons,
                    ...appState
                }
            })
        }
        
        if (state) {
            return
        }

        fetchAppState()
    }, [backendApi, host, hostIpc, state, dispatch])
    
    React.useEffect(() => {
        handleAppState()
    }, [handleAppState])
    
    return (
        <AppStateContext.Provider value={state}>
            <AppStateDispatchContext.Provider value={dispatch}>
                {children}
            </AppStateDispatchContext.Provider>
        </AppStateContext.Provider>
    )
}