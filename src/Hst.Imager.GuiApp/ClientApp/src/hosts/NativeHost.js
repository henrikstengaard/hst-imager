export class NativeHost {
    constructor({
                    backendBaseUrl = ''
                } = {}) {
        this.backendBaseUrl = backendBaseUrl;
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
    }

    async maximizeWindow() {
    }

    async restoreWindow() {
    }

    async closeWindow() {
    }

    async openExternal({ url }) {
        const response = await fetch(`${this.backendBaseUrl}api/open-external`, {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                url
            })
        });

        if (!response.ok) {
            console.error('Failed to open external')
        }
    }
}