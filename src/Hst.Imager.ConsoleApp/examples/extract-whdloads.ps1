# Extract WHDLoads
# ----------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2023-04-21
#
# A powershell script to extract whdloads .lha files recursively from a directory
# to an amiga harddisk file and install minimal Amiga OS 3.1 from adf files using
# Hst Imager console.

trap {
    Write-Error "Exception occured: $($_.Exception)"
    exit 1
}

# add winform assembly for dialogs
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName Microsoft.VisualBasic

# show question dialog using winforms
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

# show folder browser dialog using winforms
function FolderBrowserDialog($title, $directory, $showNewFolderButton)
{
    $folderBrowserDialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $folderBrowserDialog.Description = $title
    $folderBrowserDialog.SelectedPath = $directory
    $folderBrowserDialog.ShowNewFolderButton = $showNewFolderButton
    $result = $folderBrowserDialog.ShowDialog()

    if($result -ne "OK")
    {
        return $null
    }

    return $folderBrowserDialog.SelectedPath
}

# paths
$currentPath = (Get-Location).Path
$scriptPath = Split-Path -Parent $PSCommandPath
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

# select whdloads directory to extract
$whdloadsPath = FolderBrowserDialog "Select WHDLoads directory to extract" $defaultImageDir $false

# return, if whdloads directory is not defined
if (!$whdloadsPath -or $whdloadsPath -eq '')
{
    throw "WHDLoads directory not selected"
}

# show create image question dialog
$createImage = QuestionDialog 'Create image' "Do you want to create a new image file?`r`n`r`nIf No then existing image file can be selected."

$imagePath = $null
if ($createImage)
{
    # set image path
    $imagePath = Join-Path $currentPath -ChildPath "whdloads.vhd"
    Write-Output ("Creating image file '{0}'" -f $imagePath)

    # create blank image of 16gb in size
    & $hstImagerPath blank "$imagePath" "16gb"

    # initialize rigid disk block for entire disk
    & $hstImagerPath rdb init "$imagePath"

    # add rdb file system pfs3aio with dos type PDS3
    & $hstImagerPath rdb fs add "$imagePath" pfs3aio PDS3

    # add rdb partition of 500mb disk space with device name "DH0" and set bootable
    & $hstImagerPath rdb part add "$imagePath" DH0 PDS3 500mb --bootable

    # add rdb partition of remaining disk space with device name "DH1"
    & $hstImagerPath rdb part add "$imagePath" DH1 PDS3 *

    # format rdb partition number 1 with volume name "Workbench"
    & $hstImagerPath rdb part format "$imagePath" 1 Workbench

    # format rdb partition number 2 with volume name "Work"
    & $hstImagerPath rdb part format "$imagePath" 2 Work
}
else
{
    $imagePath = OpenFileDialog "Select image file to extract WHDLoads to" $currentPath "Img Files|*.img|HDF Files|*.hdf|VHD Files|*.vhd|All Files|*.*"

    # error, if image path is not found
    if (!(Test-Path $imagePath))
    {
        Write-Error ("Image path '{0}' not found" -f $imagePath)
        exit 1
    }
}

# show install minimal whdload question dialog
if (QuestionDialog 'Install minimal WHDLoad' "Do you want to install minimal WHDLoad?")
{
    # run install minimal whdload script
    & (Join-Path $scriptPath -ChildPath 'install-minimal-whdload.ps1') -imagePath $imagePath -noAmigaOs
}

# enter target directory whdloads are extracted to
$targetDir = [Microsoft.VisualBasic.Interaction]::InputBox("Target directory WHDLoads are extracted to (enter = DH1\WHDLoads)", "Target directory WHDLoads")

# set default target directory, if not set or empty
if (!$targetDir -or $targetDir -eq '')
{
    $targetDir = 'DH1\WHDLoads'
}

# find .lha and .zip files whdloads directory
$whdloadFiles = @()
$whdloadFiles += Get-ChildItem $whdloadsPath -Recurse | Where-Object { $_.Name -match '.*\.lha|.*\.zip' }

# extract each whdload file to image
foreach ($whdloadFile in $whdloadFiles)
{
    $indexDir = (Split-Path $whdloadFile.FullName -Leaf).Substring(0,1).ToUpper()
    
    if ($indexDir -match '^[0-9]')
    {
        $indexDir = '0'
    }

    Write-Output $whdloadFile.Name
    & $hstImagerPath fs extract $whdloadFile.FullName "$imagePath\rdb\$targetDir\$indexDir" --quiet
}

Write-Output "Done"