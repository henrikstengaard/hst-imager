# Install AmigaOS 3.1
# -------------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2023-07-10
#
# A powershell script to install AmigaOS 3.1 adf files to an amiga harddisk
# image file using Hst Imager console and Hst Amiga console.
#
# Requirements:
# - Hst Amiga
# - AmigaOS 3.1 adf files
# - AmigaOS 3.1.4+ install adf for DOS7, if creating new image with DOS7 dostype.

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

# amigaos 3.1 files
$amigaOs31Files = @(
    @{
        'Filename' = 'amiga-os-310-install.adf';
        'Name' = 'AmigaOS 3.1 Install Disk'
    },
    @{
        'Filename' = 'amiga-os-310-workbench.adf';
        'Name' = 'AmigaOS 3.1 Workbench Disk'
    },
    @{
        'Filename' = 'amiga-os-310-extras.adf';
        'Name' = 'AmigaOS 3.1 Extras Disk'
    },
    @{
        'Filename' = 'amiga-os-310-locale.adf';
        'Name' = 'AmigaOS 3.1 Locale Disk'
    },
    @{
        'Filename' = 'amiga-os-310-fonts.adf';
        'Name' = 'AmigaOS 3.1 Fonts Disk'
    },
    @{
        'Filename' = 'amiga-os-310-storage.adf';
        'Name' = 'AmigaOS 3.1 Storage Disk'
    }
)

# get amigaos 3.1 files copied to current path
GetAdfFiles $amigaOs31Files $currentPath

# amigaos 3.1 adf paths
$workbenchAdfPath = Join-Path $currentPath -ChildPath "amiga-os-310-workbench.adf"
$localeAdfPath = Join-Path $currentPath -ChildPath "amiga-os-310-locale.adf"
$extrasAdfPath = Join-Path $currentPath -ChildPath "amiga-os-310-extras.adf"
$fontsAdfPath = Join-Path $currentPath -ChildPath "amiga-os-310-fonts.adf"
$installAdfPath = Join-Path $currentPath -ChildPath "amiga-os-310-install.adf"
$storageAdfPath = Join-Path $currentPath -ChildPath "amiga-os-310-storage.adf"

# show create image question dialog
$createImage = QuestionDialog 'Create image' "Do you want to create a new image file?`r`n`r`nIf No then existing image file can be selected."

$imagePath = $null
if ($createImage)
{
    # set image path
    $imagePath = Join-Path $currentPath -ChildPath "amigaos-3.1.vhd"

    CreateImage $hstImagerPath $imagePath "16gb"
}
else
{
    $imagePath = OpenFileDialog "Select image file to install AmigsOS 3.1 to" $currentPath "Hard disk image files|*.img;*.hdf;*.vhd|All Files|*.*"

    # error, if image path is not found
    if (!(Test-Path $imagePath))
    {
        Write-Error ("Image path '{0}' not found" -f $imagePath)
        exit 1
    }
}

# extract workbench adf to image file
& $hstImagerPath fs extract $workbenchAdfPath "$imagePath\rdb\dh0"

# extract locale adf to image file
& $hstImagerPath fs extract $localeAdfPath "$imagePath\rdb\dh0\Locale"

# extract extras adf to image file
& $hstImagerPath fs extract $extrasAdfPath "$imagePath\rdb\dh0"

# extract fonts adf to image file
& $hstImagerPath fs extract $fontsAdfPath "$imagePath\rdb\dh0\Fonts"

# extract install adf to image file
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "HDTools\BRU") "$imagePath\rdb\dh0\Tools"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "HDTools\HDBackup") "$imagePath\rdb\dh0\Tools"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "HDTools\HDBackup.help") "$imagePath\rdb\dh0\Tools"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "HDTools\HDToolBox") "$imagePath\rdb\dh0\Tools"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "HDTools\HDBackup.info") "$imagePath\rdb\dh0\Tools"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "HDTools\HDToolBox.info") "$imagePath\rdb\dh0\Tools"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "HDTools\S\BRUtab") "$imagePath\rdb\dh0\S"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "HDTools\S\HDBackup.config") "$imagePath\rdb\dh0\S"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "L\FastFileSystem") "$imagePath\rdb\dh0\L"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "Libs\*.library") "$imagePath\rdb\dh0\Libs"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "Update\Disk.info") "$imagePath\rdb\dh0"

# extract storage adf to image file
& $hstImagerPath fs extract $storageAdfPath "$imagePath\rdb\dh0\Storage"

# copy icons from image file to local directory
& $hstImagerPath fs copy "$imagePath\rdb\dh0\*.info" (Join-Path $currentPath -ChildPath "icons") --recursive
Copy-Item (Join-Path $currentPath -ChildPath "icons\storage\Printers.info") (Join-Path $currentPath -ChildPath "icons\Storage.info") -Force

# update icons
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Prefs.info") -x 12 -y 20
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Prefs\Printer.info") -x 160 -y 48

& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Utilities.info") -x 98 -y 4
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Utilities\Clock.info") -x 91 -y 11
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Utilities\MultiView.info") -x 7 -y 4

& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Tools.info") -x 98 -y 38
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Tools\IconEdit.info") -x 111 -y 4
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Tools\Commodities\Blanker.info") -x 8 -y 84
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Tools\Commodities\ClickToFront.info") -x 99 -y 4
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Tools\Commodities\CrossDOS.info") -x 99 -y 44
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Tools\Commodities\Exchange.info") -x 8 -y 4
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Tools\Commodities\FKey.info") -x 99 -y 84

& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\System.info") -x 184 -y 4
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\WBStartup.info") -x 184 -y 38
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Devs.info") -x 270 -y 4
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Storage.info") -x 270 -y 38 -dx 480 -dy 77 -dw 107 -dh 199
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Storage\Monitors.info") -x 10 -y 106 -dx 480 -dy 77 -dw 107 -dh 199
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Storage\Printers.info") -x 10 -y 140 -dx 480 -dy 77 -dw 107 -dh 199
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Expansion.info") -x 356 -y 20
& $hstAmigaPath icon update (Join-Path $currentPath -ChildPath "icons\Disk.info") -dx 28 -dy 29 -dw 452 -dh 93

# copy icons from local directory to image file
& $hstImagerPath fs copy (Join-Path $currentPath -ChildPath "icons") "$imagePath\rdb\dh0" --recursive
