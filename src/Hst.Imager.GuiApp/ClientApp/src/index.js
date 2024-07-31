import './index.css'
import React from 'react'
import ReactDOM from 'react-dom'
import { HashRouter } from 'react-router-dom'
import { createTheme, ThemeProvider as MuiThemeProvider } from '@mui/material'
import App from './App'
import registerServiceWorker from './registerServiceWorker'
import {AppStateProvider} from "./components/AppStateContext";
import {BackendApiProvider} from "./components/BackendApiContext";

const frontendBaseUrl = document.getElementsByTagName('base')[0].getAttribute('href');
console.log(`frontend base url = '${frontendBaseUrl}'`);

let backendBaseUrl =  frontendBaseUrl;

let host = 'native';
const hostMetaTag = document.querySelector("meta[name='host']")
if (hostMetaTag) {
    host = hostMetaTag.getAttribute("content") || 'native';
}

let os = 'default';
console.log(`host = '${host}'`);

if (host === 'neutralinojs') {
    if (!window.Neutralino) {
        throw new Error('Neutralino is not present');
    }

    os = window.NL_OS;
    const port = window.NL_PORT + 1;

    backendBaseUrl = `http://localhost:${port}/`;

    // initialize neutralino
    window.Neutralino.init();

    window.Neutralino.events.on("windowClose", async() => {
        const processes = await window.Neutralino.os.getSpawnedProcesses()

        processes.forEach(process => {
            switch (os)
            {
                case 'Windows':
                    window.Neutralino.os.execCommand(`taskkill /PID ${process.pid} /F`);
                    break;
                case 'Darwin':
                case 'Linux':
                    window.Neutralino.os.execCommand(`kill ${process.pid}`);
                    break;
                default:
                    throw new Error(`Unsupported operating system ${window.NL_OS}`);
            }
        });

        // stop neutralino
        window.Neutralino.app.exit();
    });

    // start hst imager app
    const hstImagerDir = window.NL_CWD;
    const hstImagerCommand = `${hstImagerDir}/Hst.Imager.Backend.exe --port ${port}`;
    window.Neutralino.os.spawnProcess(hstImagerCommand, hstImagerDir);
}

console.log(`backend base url = '${backendBaseUrl}'`);

const rootElement = document.getElementById('root');
const theme = createTheme({
    typography: {
        fontFamily: [
            // 'topazplus_a600a1200a4000Rg',
            'Segoe UI',            
            'sans-serif',
        ].join(','),
    },
    components: {
        MuiTypography: {
            styleOverrides: {
                h1: {
                    fontSize: '0.9rem'
                },
                h2: {
                    fontSize: '2rem'
                },
                h3: {
                    fontSize: '1.5rem'
                },
                h4: {
                    fontSize: '1.2rem'
                },
                h6: {
                    fontSize: '1rem'
                }
            }
        }
    }
})

ReactDOM.render(
    <BackendApiProvider backendBaseUrl={backendBaseUrl}>
        <AppStateProvider os={os} host={host}>
            <MuiThemeProvider theme={theme}>
                <HashRouter>
                    <App />
                </HashRouter>
            </MuiThemeProvider>
        </AppStateProvider>
    </BackendApiProvider>,
    rootElement);

if (host !== 'neutralinojs') {
    registerServiceWorker();
}
