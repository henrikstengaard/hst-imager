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
    faCompress,
    faEraser,
    faPlus,
    faQuestion,
    faHome,
    faSyncAlt,
    faEllipsisH,
    faBan,
    faInfo,
    faArrowLeft,
    faTimes,
    faChartLine,
    fa1,
    fa2,
    faSliders,
    faFileFragment,
    faRulerHorizontal,
    faLocationCrosshairs,
    faScaleBalanced,
    faGear,
    faRotateLeft
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
import {AppStateContext} from "./components/AppStateContext";
import {get} from "lodash";

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
    faCompress,
    faEraser,
    faPlus,
    faQuestion,
    faHome,
    fa1,
    fa2,
    faSyncAlt,
    faEllipsisH,
    faBan,
    faInfo,
    faArrowLeft,
    faTimes,
    faChartLine,
    faSliders,
    faFileFragment,
    faRulerHorizontal,
    faLocationCrosshairs,
    faScaleBalanced,
    faGear,
    faRotateLeft)

export default function App() {
    const appState = React.useContext(AppStateContext)
    const hasTitleBar = get(appState, 'hasTitleBar') || false
    
    return (
        <Box sx={{ display: 'flex' }}>
            <CssBaseline />
            {hasTitleBar && (
                <Titlebar />
            )}
            <License>
                <ProgressProvider>
                    <ProgressBackdrop>
                        <ErrorSnackBar />
                        <Navigation>
                            <Content />
                        </Navigation>
                    </ProgressBackdrop>
                </ProgressProvider>
            </License>
        </Box>
    )
}