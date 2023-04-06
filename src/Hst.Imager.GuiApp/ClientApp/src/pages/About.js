import React from 'react'
import {Alert} from "@mui/material";
import Box from "@mui/material/Box";
import Title from "../components/Title";
import Link from '@mui/material/Link'
import {ElectronIpc} from "../utils/ElectronIpc"
import {AppStateContext} from "../components/AppStateContext";
import Typography from "@mui/material/Typography";
import {HST_IMAGER_VERSION} from '../Constants'

const payPalDonateUrl = 'https://www.paypal.com/donate/?business=7DZM5VEGWWNP8&no_recurring=0&item_name=Thanks+for+your+incredible+effort+creating+HstWB+Installer+and+Hst+Imager+in+your+spare+time.+I+want+to+support+future+development.&currency_code=EUR'
const gitHubReleasesUrl = 'https://github.com/henrikstengaard/hst-imager/releases'
const gitHubIssuesUrl = 'https://github.com/henrikstengaard/hst-imager/issues'

export default function About() {
    const electronIpc = new ElectronIpc()
    const appState = React.useContext(AppStateContext)

    const openUrl = async (event, url) => {
        event.preventDefault()
        if (!appState || !appState.isElectronActive)
        {
            console.error('Open url is only available with Electron')
            return
        }
        await electronIpc.openExternal({
            url
        })
    }
    
    return (
        <Box>
            <Title
                text="About"
            />
            <Typography sx={{ mt: 1 }}>
                Hst Imager v{HST_IMAGER_VERSION}.
            </Typography>

            <Typography sx={{ mt: 1 }}>
                Hst Imager is an imaging tool to read and write disk images to and from physical drives.
                This tool can be used to create new blank images or create images of physical drives like hard disks, SSD, CF- and MicroSD-cards for backup and/or modification and then write them to physical drives.
            </Typography>

            <Alert severity="warning" sx={{ mt: 1 }}>
                Hst Imager has been tested extensively regarding it's raw disk access.
                However it's highly recommended to make a backup of your physical drive or image file, so your working with a copy in case Hst Imager might corrupt it.
                <span style={{fontWeight: 'bold'}}> YOU HAVE BEEN WARNED NOW!</span>
            </Alert>

            <Alert severity="warning" sx={{ mt: 1 }}>
                Hst Imager filters out fixed disks, so only removeable and USB attached physical drives are accessible. Be very sure to select the correct physical drive. Otherwise Hst Imager might destroy your disk and it's file system.
                Raw disk access requires administrator privileges, so you need to run as administrator or with sudo.
            </Alert>
            
            <Typography sx={{ mt: 1 }}>
                Hst Imager is created and maintained by Henrik Nørfjand Stengaard in his spare time. To support future development and appreciate your use of Hst Imager, please make a donation via <Link href="#" onClick={async (event) => openUrl(event, payPalDonateUrl)}>PayPal donate</Link>.
            </Typography>

            <Typography sx={{ mt: 1 }}>
                Latest version of Hst Imager can be downloaded from <Link href="#" onClick={async (event) => openUrl(event, gitHubReleasesUrl)}>Hst Imager Github releases</Link>.
            </Typography>
            
            <Typography sx={{ mt: 1 }}>
                Please report issues by creating a new issue at <Link href="#" onClick={async (event) => openUrl(event, gitHubIssuesUrl)}>Hst Imager Github issues</Link>.
            </Typography>
        </Box>
    )
}