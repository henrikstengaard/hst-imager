import {ElectronIpc} from "../utils/ElectronIpc"

export class ElectronHost {
    constructor({
                    backendBaseUrl = ''
                } = {}) {
        this.backendBaseUrl = backendBaseUrl;
        this.electronIpc = new ElectronIpc()
        // this.electronIpc.on({event: 'window-maximized', callback: () => setMaximized(true)})
        // this.electronIpc.on({event: 'window-unmaximized', callback: () => setMaximized(false)})
    }

    async showOpenDialog({id, title, filters, promptCreate}) {
        const response = await fetch(`${this.backendBaseUrl}api/show-open-dialog`, {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                id,
                title,
                fileFilters: filters,
                promptCreate
            })
        });

        if (!response.ok) {
            console.error('Failed to show open dialog')
        }
    }

    async showSaveDialog({id, title, filters}) {
        const response = await fetch(`${this.backendBaseUrl}api/show-save-dialog`, {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                id,
                title,
                fileFilters: filters
            })
        });
        
        if (!response.ok) {
            console.error('Failed to show save dialog')
        }
    }

    async minimizeWindow() {
        this.electronIpc.send({message: 'minimize-window'})
    }

    async maximizeWindow() {
        this.electronIpc.send({message: 'maximize-window'})
    }

    async restoreWindow() {
        this.electronIpc.send({message: 'unmaximize-window'})
    }
    
    async closeWindow() {
        this.electronIpc.send({message: 'close-window'});
    }
    
    async openExternal({ url }) {
        await this.electronIpc.openExternal({ url })
    }
}