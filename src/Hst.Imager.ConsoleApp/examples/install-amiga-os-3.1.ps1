# Install Amiga OS 3.1
# --------------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2023-04-21
#
# A powershell script to install Amiga OS 3.1 adf files to an amiga harddisk file using Hst Imager console and Hst Amiga console.

trap {
    Write-Error "Exception occured: $($_.Exception)"
    exit 1
}

# add winform assembly for dialogs
Add-Type -AssemblyName System.Windows.Forms

# question dialog
function QuestionDialog($title, $message, $icon = 'Question')
{
    $result = [System.Windows.Forms.MessageBox]::Show($message, $title, [System.Windows.Forms.MessageBoxButtons]::YesNo, $icon)

    if($result -eq "YES")
    {
        return $true
    }

    return $false
}

# show open file dialog using winforms
function OpenFileDialog($title, $directory, $filter)
{
    $openFileDialog = New-Object System.Windows.Forms.OpenFileDialog
    $openFileDialog.initialDirectory = $directory
    $openFileDialog.Filter = $filter
    $openFileDialog.FilterIndex = 0
    $openFileDialog.Multiselect = $false
    $openFileDialog.Title = $title
    $result = $openFileDialog.ShowDialog()

    if($result -ne "OK")
    {
        return $null
    }

    return $openFileDialog.FileName
}

function SelectAmigaOsAdfFile($title)
{
    $adfPath = ${Env:AMIGAFOREVERDATA}
    if ($adfPath)
    {
        $adfPath = Join-Path $adfPath -ChildPath "Shared\adf"
    }
    else
    {
        $adfPath = ${Env:USERPROFILE}
    }

    $adfFile = OpenFileDialog $title $adfPath "ADF Files|*.adf|All Files|*.*"

    if (!$adfFile -or $adfFile -eq '')
    {
        Write-Error "Adf file not selected"
        exit 1
    }

    return $adfFile
}

# paths
$currentPath = (Get-Location).Path
$scriptPath = Split-Path -Parent $PSCommandPath
$hstAmigaPath = Join-Path $currentPath -ChildPath 'hst.amiga.exe'
$hstImagerPath = Join-Path $currentPath -ChildPath 'hst.imager.exe'

# use hst imager development app, if present
$hstImagerDevPath = Join-Path $currentPath -ChildPath 'Hst.Imager.ConsoleApp.exe'
if (Test-Path $hstImagerDevPath)
{
    $hstImagerPath = $hstImagerDevPath
}

# error, if hst imager is not found
if (!(Test-Path $hstImagerPath))
{
    Write-Error ("Hst Imager file '{0}' not found" -f $hstImagerPath)
    exit 1
}

# error, if hst amiga is not found
if (!(Test-Path $hstAmigaPath))
{
    Write-Error ("Hst Amiga file '{0}' not found. Download from https://github.com/henrikstengaard/hst-amiga/releases and extract next to Hst Imager." -f $hstAmigaPath)
    exit 1
}

$workbenchAdfPath = Join-Path $scriptPath -ChildPath "amiga-os-310-workbench.adf"
$localeAdfPath = Join-Path $scriptPath -ChildPath "amiga-os-310-locale.adf"
$extrasAdfPath = Join-Path $scriptPath -ChildPath "amiga-os-310-extras.adf"
$fontsAdfPath = Join-Path $scriptPath -ChildPath "amiga-os-310-fonts.adf"
$installAdfPath = Join-Path $scriptPath -ChildPath "amiga-os-310-install.adf"
$storageAdfPath = Join-Path $scriptPath -ChildPath "amiga-os-310-storage.adf"

$srcAdfPath = $scriptPath
foreach ($adfName in @("Workbench", "Locale", "Extras", "Fonts", "Install", "Storage"))
{
    $destAdfExists = $false

    while (!$destAdfExists)
    {
        $destAdfFilename = ("amiga-os-310-{0}.adf" -f $adfName.ToLower())
        $destAdfPath = Join-Path $scriptPath -ChildPath $destAdfFilename
        $destAdfExists = Test-Path $destAdfPath

        if ($destAdfExists)
        {
            break
        }

        $adfPath = Join-Path $srcAdfPath -ChildPath $destAdfFilename
        $adfExists = Test-Path $adfPath

        if ($adfExists)
        {
            # copy detected adf path
            Copy-Item $adfPath $destAdfPath -Force
            break
        }

        $adfPath = SelectAmigaOsAdfFile ("Select Amiga OS 3.1 {0} adf file" -f $adfName)
        $adfExists = Test-Path $adfPath

        if (!$adfExists)
        {
            Write-Error ("Error: Amiga OS 3.1 {0} adf file \'{1}\' not found" -f $adfName, $adfPath)
            continue 
        }

        $srcAdfPath = Split-Path $adfPath -Parent

        # copy selected adf path
        Copy-Item $adfPath $destAdfPath -Force
        
        break
    }
}

# set image path
$imagePath = Join-Path $currentPath -ChildPath "amiga-os-310.vhd"
Write-Output ("Creating image file '{0}'" -f $imagePath)

# show use pfs3 question dialog
$usePfs3 = QuestionDialog 'Use PFS3 file system' "Do you want to use PFS3 file system?`r`n`r`nIf No then DOS3 file system is used and will be imported`r`nfrom Amiga OS install disk."

# create blank image of 500mb in size
& $hstImagerPath blank "$imagePath" "500mb"

# initialize rigid disk block for entire disk
& $hstImagerPath rdb init "$imagePath"

if ($usePfs3)
{
    # add rdb file system pfs3aio with dos type PDS3
    & $hstImagerPath rdb fs add "$imagePath" pfs3aio PDS3

    # add rdb partition of entire disk with device name "DH0" and set bootable
    & $hstImagerPath rdb part add "$imagePath" DH0 PDS3 * --bootable
}
else
{
    # add rdb file system fast file system with dos type DOS3 imported from amiga os install adf
    & $hstImagerPath rdb fs import "$imagePath" $installAdfPath --dos-type DOS3 --name FastFileSystem

    # add rdb partition of entire disk with device name "DH0" and set bootable
    & $hstImagerPath rdb part add "$imagePath" DH0 DOS3 * --bootable
}

# format rdb partition number 1 with volume name "Workbench"
& $hstImagerPath rdb part format "$imagePath" 1 Workbench


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
