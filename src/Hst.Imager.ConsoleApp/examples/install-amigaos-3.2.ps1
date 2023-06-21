# Install AmigaOS 3.2
# -------------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2023-06-21
# A powershell script to install Amiga OS 3.1 adf files to an amiga harddisk file
# using Hst Imager console and Hst Amiga console.
#
# Requirements:
# - Hst Amiga
# - AmigaOS 3.2 adf files


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
    }
)

# get amigaos 3.2 files copied to current path
GetAmigaOsFiles $amigaOs32Files $currentPath

# amigaos 3.2 adf paths
$installAdfPath = Join-Path $currentPath -ChildPath "Install3.2.adf"
$workbenchAdfPath = Join-Path $currentPath -ChildPath "Workbench3.2.adf"
$extrasAdfPath = Join-Path $currentPath -ChildPath "Extras3.2.adf"
$classesAdfPath = Join-Path $currentPath -ChildPath "Classes3.2.adf"
$fontsAdfPath = Join-Path $currentPath -ChildPath "Fonts.adf"
$storageAdfPath = Join-Path $currentPath -ChildPath "Storage3.2.adf"

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

$installPath = Join-Path $currentPath -ChildPath 'temp\install'
if (Test-Path $installPath)
{
    Remove-Item $installPath -Recurse
}

mkdir (Join-Path $installPath -ChildPath "Prefs") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Prefs/Env-Archive") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Prefs/Env-Archive/Sys") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Prefs/Env-Archive/Versions") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Prefs/Presets") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Prefs/Presets/Backdrops") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Prefs/Presets/Pointers") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Fonts") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Expansion") | Out-Null
mkdir (Join-Path $installPath -ChildPath "WBStartup") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Locale") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Locale/Catalogs") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Locale/Languages") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Locale/Countries") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Locale/Help") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Classes") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Classes/Gadgets") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Classes/DataTypes") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Classes/Images") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Devs") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Devs/Monitors") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Devs/DataTypes") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Devs/DOSDrivers") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Devs/Printers") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Devs/Keymaps") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Storage") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Storage/DOSDrivers") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Storage/Printers") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Storage/Monitors") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Storage/Keymaps") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Storage/DataTypes") | Out-Null

mkdir (Join-Path $installPath -ChildPath "Libs") | Out-Null
mkdir (Join-Path $installPath -ChildPath "Tools") | Out-Null
mkdir (Join-Path $installPath -ChildPath "System") | Out-Null

mkdir (Join-Path $installPath -ChildPath "L") | Out-Null
mkdir (Join-Path $installPath -ChildPath "S") | Out-Null

& $hstImagerPath fs copy $installPath "$imagePath\rdb\dh0" --recursive

& $hstImagerPath fs extract "$installAdfPath\C" "$imagePath\rdb\dh0\C"

& $hstImagerPath fs extract "$installAdfPath\HDTools\hd*" "$imagePath\rdb\dh0\Tools"

& $hstImagerPath fs extract "$installAdfPath\Installer" "$imagePath\rdb\dh0\System"

& $hstImagerPath fs extract "$installAdfPath\Libs\workbench.library" "$imagePath\rdb\dh0\Libs"

& $hstImagerPath fs extract "$installAdfPath\Libs\icon.library" "$imagePath\rdb\dh0\Libs"

$updatePath = Join-Path $currentPath -ChildPath "temp\update"
if (Test-Path $updatePath)
{
    Remove-Item $updatePath -Recurse
}

& $hstImagerPath fs extract "$installAdfPath\Update" "$updatePath"

# copy fastfilesystem
& $hstImagerPath fs extract "$installAdfPath\L\FastFileSystem" "$imagePath\rdb\dh0\L"



# workbench
# ---------

& $hstImagerPath fs extract "$workbenchAdfPath" "$imagePath\rdb\dh0"

# extras
# ------

#Copy >NIL: "$amigaosdisk:~(Disk.info|S)" "SYSTEMDIR:" ALL CLONE
#Copy >NIL: "$amigaosdisk:S/~(user-startup)" "SYSTEMDIR:S" ALL CLONE
& $hstImagerPath fs extract "$extrasAdfPath\*.info" "$imagePath\rdb\dh0" --recursive false
& $hstImagerPath fs extract "$extrasAdfPath\L" "$imagePath\rdb\dh0\L"
& $hstImagerPath fs extract "$extrasAdfPath\Prefs" "$imagePath\rdb\dh0\Prefs"
& $hstImagerPath fs extract "$extrasAdfPath\System" "$imagePath\rdb\dh0\System"
& $hstImagerPath fs extract "$extrasAdfPath\Tools" "$imagePath\rdb\dh0\Tools"

$sPath = Join-Path $currentPath -ChildPath "temp\s"
if (Test-Path $sPath)
{
    Remove-Item $sPath -Recurse
}
& $hstImagerPath fs extract "$extrasAdfPath\S" "$sPath"
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
$iconsPath = Join-Path $currentPath -ChildPath "temp\icons"
if (Test-Path $iconsPath)
{
    Remove-Item $iconsPath -Recurse
}

& $hstImagerPath fs copy "$imagePath\rdb\dh0\*.info" "$iconsPath" --recursive

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
