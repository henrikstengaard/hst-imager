import React from 'react';
import Box from '@mui/material/Box';
import {Route} from 'react-router';
import Start from "../pages/Start";
import Read from "../pages/Read";
import Write from "../pages/Write";
import Info from "../pages/Info";
import Convert from "../pages/Convert";
import Compare from "../pages/Compare";
import Blank from "../pages/Blank";
import Optimize from "../pages/Optimize";
import Partition from "../pages/Partition";
import About from "../pages/About";
import Settings from "../pages/Settings";
import {AppStateContext} from "./AppStateContext";
import {get} from "lodash";

export default function Content() {
    const appState = React.useContext(AppStateContext)
    const hasTitleBar = get(appState, 'hasTitleBar') || false
    
    return (
        <Box component="main" sx={{flexGrow: 1, marginTop: hasTitleBar ? '32px' : '0', p: 3}}>
            <Route exact path='/' component={Start}/>
            <Route path='/read' component={Read}/>
            <Route path='/write' component={Write}/>
            <Route path='/info' component={Info}/>
            <Route path='/convert' component={Convert}/>
            <Route path='/compare' component={Compare}/>
            <Route path='/blank' component={Blank}/>
            <Route path='/optimize' component={Optimize}/>
            <Route path='/partition' component={Partition}/>
            <Route path='/settings' component={Settings}/>
            <Route path='/about' component={About}/>
        </Box>
    )
}