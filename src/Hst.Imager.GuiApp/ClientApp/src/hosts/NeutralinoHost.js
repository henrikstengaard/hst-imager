import {HubConnectionBuilder} from "@microsoft/signalr";

export class NeutralinoHost {
    constructor({
                    os,
                    backendBaseUrl = ''
                } = {}) {
        this.os = os;
        this.connection = new HubConnectionBuilder()
            .withUrl(`${backendBaseUrl}hubs/show-dialog-result`)
            .withAutomaticReconnect()
            .build();

        this.connection.start();
    }
    
    async showOpenDialog({ id, title, filters }) {
        let entries = await window.Neutralino.os.showOpenDialog(title, {
            filters: filters
        });

        switch (this.os) {
            case 'Windows':
                entries = entries.map(entry => entry.replaceAll('/', '\\'));
                break;
            default:
                entries = entries.map(entry => entry.replaceAll('\\', '/'));
                break;
        }
        
        await this.connection.send("ShowDialogResult", {
            id,
            isSuccess: true,
            paths: entries
        });
    }

    async showSaveDialog({ id, title, filters }) {
        let entry = await window.Neutralino.os.showSaveDialog(title, {
            filters: filters
        });

        switch (this.os) {
            case 'Windows':
                entry = entry.replaceAll('/', '\\');
                break;
            default:
                entry = entry.replaceAll('\\', '/');
                break;
        }

        await this.connection.send("ShowDialogResult", {
            id,
            isSuccess: true,
            paths: [ entry ]
        });
    }

    async minimizeWindow() {
        window.Neutralino.window.minimize();
    }

    async maximizeWindow() {
        window.Neutralino.window.maximize();
    }

    async restoreWindow() {
        window.Neutralino.window.unmaximize();
    }

    async closeWindow() {
        // neutralinojs doesn't have a method for close window, app exit is used instead
        window.Neutralino.app.exit();
    }

    async openExternal({ url }) {
        window.Neutralino.os.open(url)
    }
}

