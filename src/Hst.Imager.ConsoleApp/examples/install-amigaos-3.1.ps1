# Install AmigaOS 3.1
# -------------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2025-10-21
#
# A powershell script to install AmigaOS 3.1 adf files to an amiga harddisk
# image file using Hst Imager console and Hst Amiga console.
#
# Requirements:
# - Hst Amiga
# - AmigaOS 3.1 adf files
# - AmigaOS 3.1.4+ install adf for DOS7, if creating new image with DOS7 dostype.

$ErrorActionPreference = "Stop"
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
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Locale"
& $hstImagerPath fs extract $localeAdfPath "$imagePath\rdb\dh0\Locale"

# extract extras adf to image file
& $hstImagerPath fs extract $extrasAdfPath "$imagePath\rdb\dh0"

# extract fonts adf to image file
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Fonts"
& $hstImagerPath fs extract $fontsAdfPath "$imagePath\rdb\dh0\Fonts"

# extract install adf to image file
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Tools"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "HDTools\BRU") "$imagePath\rdb\dh0\Tools"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "HDTools\HDBackup") "$imagePath\rdb\dh0\Tools"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "HDTools\HDBackup.help") "$imagePath\rdb\dh0\Tools"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "HDTools\HDToolBox") "$imagePath\rdb\dh0\Tools"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "HDTools\HDBackup.info") "$imagePath\rdb\dh0\Tools"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "HDTools\HDToolBox.info") "$imagePath\rdb\dh0\Tools"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\S"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "HDTools\S\BRUtab") "$imagePath\rdb\dh0\S"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "HDTools\S\HDBackup.config") "$imagePath\rdb\dh0\S"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\L"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "L\FastFileSystem") "$imagePath\rdb\dh0\L"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Libs"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "Libs\*.library") "$imagePath\rdb\dh0\Libs"
& $hstImagerPath fs extract (Join-Path $installAdfPath -ChildPath "Update\Disk.info") "$imagePath\rdb\dh0"

# extract storage adf to image file
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Storage"
& $hstImagerPath fs extract $storageAdfPath "$imagePath\rdb\dh0\Storage"

# create temp directory
$tempPath = Join-Path $currentPath -ChildPath "temp"
if (Test-Path $tempPath)
{
    Remove-Item $tempPath -Recurse
}

# copy icons from image file to local directory
$iconsPath = Join-Path $tempPath -ChildPath "icons"
& $hstImagerPath fs copy "$imagePath\rdb\dh0\*.info" "$iconsPath" --recursive --makedir
Copy-Item (Join-Path $iconsPath -ChildPath "storage\Printers.info") (Join-Path $iconsPath -ChildPath "Storage.info") -Force

# update icons
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Prefs.info") -x 12 -y 20
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Prefs\Printer.info") -x 160 -y 48

& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Utilities.info") -x 98 -y 4
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Utilities\Clock.info") -x 91 -y 11
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Utilities\MultiView.info") -x 7 -y 4

& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Tools.info") -x 98 -y 38
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Tools\IconEdit.info") -x 111 -y 4
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Tools\Commodities\Blanker.info") -x 8 -y 84
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Tools\Commodities\ClickToFront.info") -x 99 -y 4
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Tools\Commodities\CrossDOS.info") -x 99 -y 44
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Tools\Commodities\Exchange.info") -x 8 -y 4
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Tools\Commodities\FKey.info") -x 99 -y 84

& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "System.info") -x 184 -y 4
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "WBStartup.info") -x 184 -y 38
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Devs.info") -x 270 -y 4
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Storage.info") -x 270 -y 38 -dx 480 -dy 77 -dw 107 -dh 199
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Storage\Monitors.info") -x 10 -y 106 -dx 480 -dy 77 -dw 107 -dh 199
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Storage\Printers.info") -x 10 -y 140 -dx 480 -dy 77 -dw 107 -dh 199
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Expansion.info") -x 356 -y 20
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Disk.info") -dx 28 -dy 29 -dw 452 -dh 93

# copy icons from local directory to image file
& $hstImagerPath fs copy "$iconsPath" "$imagePath\rdb\dh0" --recursive
