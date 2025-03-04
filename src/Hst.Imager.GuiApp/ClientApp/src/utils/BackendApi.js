import {isNil} from "lodash";

export class BackendApi {
    constructor({
        baseUrl = ''
    } = {}) {
        this.baseUrl = baseUrl
    }

    async data(response) {
        const contentType = response.headers.get('content-type');
        if (contentType && contentType.indexOf('application/json') !== -1) {
            return await response.json()
        }
        return await response.text()
    }

    async send({ method, path, headers, data = null } = {}) {
        const settings = {
            method,
            headers: isNil(headers) ? {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            } : headers
        };

        if (!isNil(data)) {
            settings.body = data instanceof FormData ? data : JSON.stringify(data)
        }

        try {
            let retry = 0;
            let response = null;
            
            do {
                response = await fetch(`${this.baseUrl}${path}`, settings)

                if (response.ok) {
                    return this.data(response)
                }

                retry++
            } while(retry <= 3)

            return new Error(`An error occured sending ${method} request to ${this.baseUrl}${path} returned status ${response.status} and data '${await this.data(response)}'`)
        } catch (e) {
            return new Error(`An error occured sending ${method} request to ${this.baseUrl}${path}: ${e}`)
        }
    }

    async getAppState() {
        const response = await fetch(`${this.baseUrl}api/app-state`, {
            method: 'GET',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            }
        });

        if (!response.ok) {
            throw new Error('Failed to get app state')
        }
        
        return await response.json();
    }

    async updateLicense({ agree }) {
        const response = await fetch(`${this.baseUrl}api/license`, {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                licenseAgreed: agree,
            })
        });

        if (!response.ok) {
            throw new Error('Failed to update license')
        }
    }
    
    async updateList() {
        const response = await fetch(`${this.baseUrl}api/list`, {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            }
        });
        
        if (!response.ok) {
            throw new Error('Failed to update list')
        }
    }

    async updateInfo({ path, sourceType, byteswap }) {
        return await this.send({
            method: 'POST',
            path: `api/info`,
            data: {
                sourceType,
                path,
                byteswap
            }
        })
    }

    async startBlank({ title, path, size, compatibleSize }) {
        const response = await fetch(`${this.baseUrl}api/blank`, {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                title,
                path,
                size,
                compatibleSize
            })
        });
        
        if (!response.ok) {
            throw new Error('Failed to start blank')
        }
    }

    async startCompare({ title, sourceType, sourcePath, destinationPath, size, retries, force, byteswap }) {
        const response = await fetch(`${this.baseUrl}api/compare`, {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                title,
                sourceType,
                sourcePath,
                destinationPath,
                size,
                retries,
                force,
                byteswap
            })
        });
        if (!response.ok) {
            throw new Error('Failed to start compare')
        }
    }

    async startConvert({ title, sourcePath, destinationPath, byteswap }) {
        const response = await fetch(`${this.baseUrl}api/convert`, {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                title,
                sourcePath,
                destinationPath,
                byteswap
            })
        });
        
        if (!response.ok) {
            throw new Error('Failed to start convert')
        }
    }

    async startOptimize({ title, path, size }) {
        const response = await fetch(`${this.baseUrl}api/optimize`, {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                title,
                path,
                size
            })
        });
        
        if (!response.ok) {
            throw new Error('Failed to optimize')
        }
    }

    async startRead({ title, sourcePath, destinationPath, size, retries, verify, force, byteswap }) {
        const response = await fetch(`${this.baseUrl}api/read`, {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                title,
                sourcePath,
                destinationPath,
                size,
                retries,
                verify,
                force,
                byteswap
            })
        });
        
        if (!response.ok) {
            throw new Error('Failed to start read')
        }
    }

    async startWrite({ title, sourcePath, destinationPath, size, retries, verify, force, byteswap, skipZeroFilled }) {
        const response = await fetch(`${this.baseUrl}api/write`, {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                title,
                sourcePath,
                destinationPath,
                size,
                retries,
                verify,
                force,
                byteswap,
                skipZeroFilled
            })
        });
        
        if (!response.ok) {
            throw new Error('Failed to start write')
        }
    }

    async startFormat({ title, path, formatType, fileSystem, fileSystemPath, size, byteSwap }) {
        const response = await fetch(`${this.baseUrl}api/format`, {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                title,
                path,
                formatType,
                fileSystem,
                fileSystemPath,
                size,
                byteSwap
            })
        });

        if (!response.ok) {
            throw new Error('Failed to start format')
        }
    }
    
    async updateSettings(settings) {
        const response = await fetch(`${this.baseUrl}api/settings`, {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(settings)
        });
        
        if (!response.ok) {
            throw new Error('Failed to update settings')
        }
    }

    async cancelTask() {
        const response = await fetch(`${this.baseUrl}api/cancel`, {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            }
        });

        if (!response.ok) {
            throw new Error("Failed to cancel task")
        }
    }
}