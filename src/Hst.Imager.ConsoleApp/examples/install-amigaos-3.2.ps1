# Install AmigaOS 3.2
# -------------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2025-10-21
#
# A powershell script to install Amiga OS 3.2 adf files to an amiga harddisk file
# using Hst Imager console and Hst Amiga console.
#
# Requirements:
# - Hst Amiga
# - AmigaOS 3.2 adf files

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

# amigaos 3.2 files
$amigaOs32Files = @(
    @{
        'Filename' = 'Install3.2.adf';
        'Name' = 'AmigaOS 3.2 Install Disk'
    },
    @{
        'Filename' = 'Workbench3.2.adf';
        'Name' = 'AmigaOS 3.2 Workbench Disk'
    },
    @{
        'Filename' = 'Extras3.2.adf';
        'Name' = 'AmigaOS 3.2 Extras Disk'
    },
    @{
        'Filename' = 'Classes3.2.adf';
        'Name' = 'AmigaOS 3.2 Classes Disk'
    },
    @{
        'Filename' = 'Fonts.adf';
        'Name' = 'AmigaOS 3.2 Fonts Disk'
    },
    @{
        'Filename' = 'Storage3.2.adf';
        'Name' = 'AmigaOS 3.2 Storage Disk'
    },
    @{
        'Filename' = 'DiskDoctor.adf';
        'Name' = 'AmigaOS 3.2 Disk Doctor'
    },
    @{
        'Filename' = 'MMULibs.adf';
        'Name' = 'AmigaOS 3.2 MMULibs'
    }
)

# get amigaos 3.2 files copied to current path
GetAdfFiles $amigaOs32Files $currentPath

# amigaos 3.2 adf paths
$installAdfPath = Join-Path $currentPath -ChildPath "Install3.2.adf"
$workbenchAdfPath = Join-Path $currentPath -ChildPath "Workbench3.2.adf"
$extrasAdfPath = Join-Path $currentPath -ChildPath "Extras3.2.adf"
$classesAdfPath = Join-Path $currentPath -ChildPath "Classes3.2.adf"
$fontsAdfPath = Join-Path $currentPath -ChildPath "Fonts.adf"
$storageAdfPath = Join-Path $currentPath -ChildPath "Storage3.2.adf"
$diskDoctorAdfPath = Join-Path $currentPath -ChildPath "DiskDoctor.adf"
$mmuLibsAdfPath = Join-Path $currentPath -ChildPath "MMULibs.adf"

# show create image question dialog
$createImage = QuestionDialog 'Create image' "Do you want to create a new image file?`r`n`r`nIf No then existing image file can be selected."

$imagePath = $null
if ($createImage)
{
    # set image path
    $imagePath = Join-Path $currentPath -ChildPath "amigaos-3.2.vhd"

    CreateImage $hstImagerPath $imagePath "16gb"
}
else
{
    $imagePath = OpenFileDialog "Select image file to install AmigsOS 3.2 to" $currentPath "Hard disk image files|*.img;*.hdf;*.vhd|All Files|*.*"

    # error, if image path is not found
    if (!(Test-Path $imagePath))
    {
        Write-Error ("Image path '{0}' not found" -f $imagePath)
        exit 1
    }
}

# install
# -------

& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Prefs"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Prefs\Env-Archive"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Prefs\Env-Archive\Sys"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Prefs\Env-Archive\Versions"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Prefs\Presets"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Prefs\Presets\Backdrops"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Prefs\Presets\Pointers"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Fonts"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Expansion"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\WBStartup"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Locale"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Locale\Catalogs"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Locale\Languages"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Locale\Countries"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Locale\Help"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Classes"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Classes\Gadgets"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Classes\DataTypes"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Classes\Images"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Devs"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Devs\Monitors"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Devs\DataTypes"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Devs\DOSDrivers"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Devs\Printers"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Devs\Keymaps"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Storage"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Storage\DOSDrivers"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Storage\Printers"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Storage\Monitors"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Storage\Keymaps"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Storage\DataTypes"

& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Libs"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\Tools"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\System"

& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\C"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\L"
& $hstImagerPath fs mkdir "$imagePath\rdb\dh0\S"

& $hstImagerPath fs extract "$installAdfPath\C" "$imagePath\rdb\dh0\C"

& $hstImagerPath fs extract "$installAdfPath\HDTools\hd*" "$imagePath\rdb\dh0\Tools"

& $hstImagerPath fs extract "$installAdfPath\Installer" "$imagePath\rdb\dh0\System"

& $hstImagerPath fs extract "$installAdfPath\Libs\workbench.library" "$imagePath\rdb\dh0\Libs"

& $hstImagerPath fs extract "$installAdfPath\Libs\icon.library" "$imagePath\rdb\dh0\Libs"

# create temp directory
$tempPath = Join-Path $currentPath -ChildPath "temp"
if (Test-Path $tempPath)
{
    Remove-Item $tempPath -Recurse
}

$updatePath = Join-Path $tempPath -ChildPath "update"
& $hstImagerPath fs extract "$installAdfPath\Update" "$updatePath" --makedir

# copy fastfilesystem
& $hstImagerPath fs extract "$installAdfPath\L\FastFileSystem" "$imagePath\rdb\dh0\L"



# workbench
# ---------

& $hstImagerPath fs extract "$workbenchAdfPath" "$imagePath\rdb\dh0"

# extras
# ------

& $hstImagerPath fs extract "$extrasAdfPath\*.info" "$imagePath\rdb\dh0" --recursive false
& $hstImagerPath fs extract "$extrasAdfPath\L" "$imagePath\rdb\dh0\L"
& $hstImagerPath fs extract "$extrasAdfPath\Prefs" "$imagePath\rdb\dh0\Prefs"
& $hstImagerPath fs extract "$extrasAdfPath\System" "$imagePath\rdb\dh0\System"
& $hstImagerPath fs extract "$extrasAdfPath\Tools" "$imagePath\rdb\dh0\Tools"

$sPath = Join-Path $tempPath -ChildPath "s"
& $hstImagerPath fs extract "$extrasAdfPath\S" "$sPath" --makedir
Remove-Item (Join-Path $sPath -ChildPath "User-startup") -Recurse
    
& $hstImagerPath fs copy "$sPath" "$imagePath\rdb\dh0\S"

# classes
# -------

& $hstImagerPath fs extract "$classesAdfPath" "$imagePath\rdb\dh0"

# fonts
# -----

& $hstImagerPath fs extract "$fontsAdfPath" "$imagePath\rdb\dh0\Fonts"

# storage
# -------

& $hstImagerPath fs extract "$storageAdfPath\DataTypes.info" "$imagePath\rdb\dh0\Storage"
& $hstImagerPath fs extract "$storageAdfPath\DOSDrivers.info" "$imagePath\rdb\dh0\Storage"
& $hstImagerPath fs extract "$storageAdfPath\Keymaps.info" "$imagePath\rdb\dh0\Storage"
& $hstImagerPath fs extract "$storageAdfPath\Monitors.info" "$imagePath\rdb\dh0\Storage"
& $hstImagerPath fs extract "$storageAdfPath\Printers.info" "$imagePath\rdb\dh0\Storage"

& $hstImagerPath fs extract "$storageAdfPath\Classes\DataTypes" "$imagePath\rdb\dh0\Classes\DataTypes"

& $hstImagerPath fs extract "$storageAdfPath\C" "$imagePath\rdb\dh0\C"

& $hstImagerPath fs extract "$storageAdfPath\DefIcons\*.info" "$imagePath\rdb\dh0\Prefs\Env-Archive\Sys"

& $hstImagerPath fs extract "$storageAdfPath\Presets\Pointers" "$imagePath\rdb\dh0\Prefs\Presets\Pointers"

& $hstImagerPath fs extract "$storageAdfPath\Monitors" "$imagePath\rdb\dh0\Storage\Monitors"

& $hstImagerPath fs extract "$storageAdfPath\DOSDrivers" "$imagePath\rdb\dh0\Storage\DOSDrivers"

& $hstImagerPath fs extract "$storageAdfPath\WBStartup" "$imagePath\rdb\dh0\WBStartup"

& $hstImagerPath fs extract "$storageAdfPath\Env-Archive\deficons.prefs" "$imagePath\rdb\dh0\Prefs\Env-Archive"

& $hstImagerPath fs extract "$storageAdfPath\Env-Archive\Pointer.prefs" "$imagePath\rdb\dh0\Prefs\Env-Archive\Sys"

& $hstImagerPath fs extract "$storageAdfPath\Printers" "$imagePath\rdb\dh0\Devs\Printers"

& $hstImagerPath fs extract "$storageAdfPath\Keymaps" "$imagePath\rdb\dh0\Devs\Keymaps"

& $hstImagerPath fs extract "$storageAdfPath\LIBS" "$imagePath\rdb\dh0\Libs"

# finalize
# --------

# copy disk.info
& $hstImagerPath fs copy (Join-Path $updatePath -ChildPath "disk.info") "$imagePath\rdb\dh0"

# copy release to versions
& $hstImagerPath fs copy (Join-Path $updatePath -ChildPath "Release") "$imagePath\rdb\dh0\Prefs\Env-Archive\Versions" --recursive

# copy startup-sequence
$startupHardDrivePath = Join-Path $updatePath -ChildPath "Startup-HardDrive"
$startupSequencePath = Join-Path $updatePath -ChildPath "Startup-sequence"
if (Test-Path $startupSequencePath)
{
    Remove-Item $startupSequencePath
}
Rename-Item -Path $startupHardDrivePath -NewName "Startup-sequence"
& $hstImagerPath fs copy "$startupSequencePath" "$imagePath\rdb\dh0\S"

# clean up
# --------

# copy icons from image file to local directory
$iconsPath = Join-Path $tempPath -ChildPath "icons"
& $hstImagerPath fs copy "$imagePath\rdb\dh0\*.info" "$iconsPath" --recursive --makedir

# update icons
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Prefs.info") -x 12 -y 20
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Prefs\Printer.info") -x 160 -y 48

& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Utilities.info") -x 98 -y 4
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Utilities\Clock.info") -x 91 -y 11
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Utilities\MultiView.info") -x 11 -y 11

& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Tools.info") -x 98 -y 38
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Tools\IconEdit.info") -x 111 -y 45
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Tools\HDToolBox.info") -x 202 -y 4

& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "System.info") -x 184 -y 4 -dh 150
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "WBStartup.info") -x 184 -y 38
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Devs.info") -x 270 -y 4

Copy-Item (Join-Path $iconsPath -ChildPath "Devs.info") (Join-Path $iconsPath -ChildPath "Storage.info") -Force

& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Storage.info") -x 270 -y 38 -dx 480 -dy 77 -dw 110 -dh 199
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Storage\Monitors.info") -dx 156 -dy 77 -dw 270 -dh 199
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Storage\Printers.info") -dx 480 -dy 77 -dw 107 -dh 199
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Expansion.info") -x 356 -y 20
& $hstAmigaPath icon update (Join-Path $iconsPath -ChildPath "Disk.info") -dx 28 -dy 29 -dw 462 -dh 103

# copy icons from local directory to image file
& $hstImagerPath fs copy "$iconsPath" "$imagePath\rdb\dh0" --recursive

# copy files from disk doctor for mounting adf in amigaos
if (Test-Path $diskDoctorAdfPath)
{
    & $hstImagerPath fs extract "$diskDoctorAdfPath\C\DAControl" "$imagePath\rdb\dh0\C"
    & $hstImagerPath fs extract "$diskDoctorAdfPath\Devs\trackfile.device" "$imagePath\rdb\dh0\Devs"
}

# copy files from mmulibs
if (Test-Path $mmuLibsAdfPath)
{
    & $hstImagerPath fs extract "$mmuLibsAdfPath\C" "$imagePath\rdb\dh0\C"
    & $hstImagerPath fs extract "$mmuLibsAdfPath\Libs" "$imagePath\rdb\dh0\Libs" --recursive
    & $hstImagerPath fs extract "$mmuLibsAdfPath\Locale" "$imagePath\rdb\dh0\Locale" --recursive
}

Write-Host "Done"
