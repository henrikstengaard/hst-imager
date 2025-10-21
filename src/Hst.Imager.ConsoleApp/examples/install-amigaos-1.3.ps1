# Install AmigaOS 1.3
# -------------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2025-07-26
#
# A powershell script to install AmigaOS 1.3 adf files to an amiga harddisk
# image file using Hst Imager console.
#
# Requirements:
# - Hst Amiga
# - AmigaOS 1.3 adf files
# - Image file to install AmigaOS 1.3 to.

trap {
    Write-Error "Exception occured: $($_.Exception)"
    exit 1
}

# paths
$currentPath = (Get-Location).Path
$scriptPath = Split-Path -Parent $PSCommandPath

# include shared script
. (Join-Path $scriptPath -ChildPath 'shared.ps1')

# get hst imager and hst amiga paths
$hstImagerPath = GetHstImagerPath $scriptPath
$hstAmigaPath = GetHstAmigaPath $scriptPath

# amigaos 1.3 files
$amigaOs13Files = @(
    @{
        'Filename' = 'amiga-os-134-extras.adf';
        'Name' = 'AmigaOS 1.3 Extras Disk'
    },
    @{
        'Filename' = 'amiga-os-134-workbench.adf';
        'Name' = 'AmigaOS 1.3 Workbench Disk'
    }
)

# get amigaos 1.3 files copied to current path
GetAdfFiles $amigaOs13Files $currentPath

# amigaos 1.3 adf paths
$workbenchAdfPath = Join-Path $currentPath -ChildPath 'amiga-os-134-workbench.adf'
$extrasAdfPath = Join-Path $currentPath -ChildPath 'amiga-os-134-extras.adf'

# select image file to install AmigaOS 1.3 to
$imagePath = OpenFileDialog "Select image file to install AmigsOS 1.3 to" $currentPath "Hard disk image files|*.img;*.hdf;*.vhd|All Files|*.*"

# error, if image path is not found
if (!(Test-Path $imagePath))
{
    Write-Error ("Image path '{0}' not found" -f $imagePath)
    exit 1
}

# extract workbench adf to image file
& $hstImagerPath fs extract $workbenchAdfPath "$imagePath\rdb\dh0"

# extract extras adf to image file
& $hstImagerPath fs extract $extrasAdfPath "$imagePath\rdb\dh0"