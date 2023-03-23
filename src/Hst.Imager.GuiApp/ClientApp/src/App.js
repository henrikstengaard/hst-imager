import React from 'react';
import {library} from '@fortawesome/fontawesome-svg-core'
import {
    faCog,
    faToolbox,
    faUpload,
    faDownload,
    faMagic,
    faLongArrowAltRight,
    faHdd,
    faFile,
    faExchangeAlt,
    faBars,
    faWindowMinimize,
    faWindowMaximize,
    faWindowRestore,
    faWindowClose,
    faChevronLeft,
    faChevronRight,
    faChevronUp,
    faChevronDown,
    faCheck,
    faPlus,
    faQuestion,
    faHome,
    faSyncAlt,
    faEllipsisH,
    faBan,
    faInfo,
    faArrowLeft,
    faTimes,
    faChartLine
} from '@fortawesome/free-solid-svg-icons'
import Box from '@mui/material/Box'
import CssBaseline from '@mui/material/CssBaseline'
import ProgressBackdrop from "./components/ProgressBackdrop"
import './custom.css'
import Titlebar from "./components/Titlebar";
import Navigation from "./components/Navigation";
import Content from "./components/Content";
import {ProgressProvider} from "./components/ProgressContext";
import ErrorSnackBar from "./components/ErrorSnackBar";
import License from "./components/License";

library.add(
    faCog,
    faToolbox,
    faUpload, faDownload, faMagic, faHdd, faFile, faLongArrowAltRight, 
    faExchangeAlt, faBars, faWindowMinimize, faWindowMaximize, faWindowRestore, faWindowClose,
    faChevronLeft,
    faChevronRight,
    faChevronUp,
    faChevronDown,
    faCheck,
    faPlus,
    faQuestion,
    faHome,
    faSyncAlt,
    faEllipsisH,
    faBan,
    faInfo,
    faArrowLeft,
    faTimes,
    faChartLine)

export default function App() {
    return (
        <Box sx={{ display: 'flex' }}>
            <CssBaseline />
            <Titlebar />
            <License>
                <ProgressProvider>
                    <ProgressBackdrop>
                        <ErrorSnackBar />
                        <Navigation />
                        <Content />
                    </ProgressBackdrop>
                </ProgressProvider>
            </License>
        </Box>
    )
}