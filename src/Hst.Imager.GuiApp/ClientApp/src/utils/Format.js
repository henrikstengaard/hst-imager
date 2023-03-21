import moment from "moment"

export const formatBytes = (bytes, decimals = 1) => {
    if (bytes === 0) return '0 B';

    const k = 1024;
    const dm = decimals < 0 ? 0 : decimals;
    const units = ['B', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'];

    let unit = Math.floor(Math.log(bytes) / Math.log(k));
    let formattedSize = parseFloat((bytes / Math.pow(k, unit)).toFixed(dm))
    if (formattedSize >= 1000 && unit < units.length - 1) {
        formattedSize = (formattedSize / 1000).toFixed(dm)
        unit++
    }
    
    return `${formattedSize} ${units[unit]}`;
}

function pad(num, size) {
    num = num.toString();
    while (num.length < size) num = "0" + num;
    return num;
}

export const formatMilliseconds = (milliseconds) => {
    const duration = moment.duration(milliseconds)

    const parts = []
    const days = duration.days()
    if (days > 0) {
        parts.push(pad(days, 2))
    }
    
    const hours = duration.hours()
    if (hours > 0) {
        parts.push(pad(hours, 2))
    }
    
    const minutes = duration.minutes()
    parts.push(pad(minutes, 2))

    const seconds = duration.seconds()
    parts.push(pad(seconds, 2))
    
    return parts.join(':')
}

export const formatMillisecondsReadable = (milliseconds) => {
    const duration = moment.duration(milliseconds)

    const parts = []
    const days = duration.days()
    if (days > 0) {
        parts.push(`${days} day${days > 1 ? 's' : ''}`)
    }

    const hours = duration.hours()
    if (hours > 0) {
        parts.push(`${hours} hour${hours > 1 ? 's' : ''}`)
    }

    const minutes = duration.minutes()
    if (minutes < 1) {
        const seconds = duration.seconds()
        parts.push(`${seconds} second${seconds > 1 ? 's' : ''}`)
    }
    else {
        parts.push(`${minutes} minute${minutes > 1 ? 's' : ''}`)
    }

    return parts.join(', ')
}