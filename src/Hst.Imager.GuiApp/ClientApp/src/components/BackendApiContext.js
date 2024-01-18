import React from 'react'
import {BackendApi} from "../utils/BackendApi";

export const BackendApiStateContext = React.createContext(null)

function init({ backendBaseUrl }) {
    return {
        backendBaseUrl,
        backendApi: new BackendApi({ baseUrl: backendBaseUrl }),
    }
}

export function BackendApiProvider({ backendBaseUrl, children}) {
    const state = init({ backendBaseUrl });

    return (
        <BackendApiStateContext.Provider value={state}>
            {children}
        </BackendApiStateContext.Provider>
    )
}