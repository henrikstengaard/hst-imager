# Extract WHDLoads
# ----------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2025-12-01
#
# A powershell script to extract whdloads .lha files recursively from a directory
# to an amiga harddisk file and install minimal Amiga OS 3.1 from adf files using
# Hst Imager console.
#
# Requirements:
# - WHDload .lha and .zip files.
# - AmigaOS 3.1.4+ install adf for DOS7, if creating new image with DOS7 dostype.

trap {
    Write-Error ("Exception occured: $($_.Exception.ToString())")
    exit 1
}

# paths
$currentPath = (Get-Location).Path
$scriptPath = Split-Path -Parent $PSCommandPath

# include shared script
. (Join-Path $scriptPath -ChildPath 'shared.ps1')

# get hst imager path
$hstImagerPath = GetHstImagerPath $scriptPath

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
    
    CreateImage $hstImagerPath $imagePath "16gb"
}
else
{
    $imagePath = OpenFileDialog "Select image file to extract WHDLoads to" $currentPath "Hard disk image files|*.img;*.hdf;*.vhd|All Files|*.*"

    # error, if image path is not found
    if (!(Test-Path $imagePath))
    {
        Write-Error ("Image path '{0}' not found" -f $imagePath)
        exit 1
    }
}

# show install minimal whdload question dialog
if (QuestionDialog 'Install minimal WHDLoad' "Do you want to install minimal WHDLoad (WHDLoad+SKick+Kickstarts+IconLib)?")
{
    # install minimal whdload 
    InstallMinimalWhdload $hstImagerPath $imagePath
}

# enter target directory whdloads are extracted to
$targetDir = [Microsoft.VisualBasic.Interaction]::InputBox("Target directory WHDLoads are extracted to (blank and OK = DH1\WHDLoads)", "Target directory WHDLoads")

# set default target directory, if not set or empty
if (!$targetDir -or $targetDir -eq '')
{
    $targetDir = 'DH1\WHDLoads'
}

# create target directory
& $hstImagerPath fs mkdir "$imagePath\rdb\$targetDir"

# find .lha and .zip files whdloads directory
$whdloadFiles = @()
$whdloadFiles += Get-ChildItem $whdloadsPath -Recurse | Where-Object { $_.Name -match '.*\.lha|.*\.lzx|.*\.zip' }

$indexDirsCreated = @{}

# extract each whdload file to image
foreach ($whdloadFile in $whdloadFiles)
{
    $indexDir = (Split-Path $whdloadFile.FullName -Leaf).Substring(0,1).ToUpper()
    
    if ($indexDir -match '^[0-9]')
    {
        $indexDir = '0'
    }

    Write-Output $whdloadFile.Name
    
    # create index directory, if not created
    if (-not $indexDirsCreated.ContainsKey($indexDir))
    {
        & $hstImagerPath fs mkdir "$imagePath\rdb\$targetDir\$indexDir"
        $indexDirsCreated[$indexDir] = $true
    }

    # extract whdload file to image file
    & $hstImagerPath fs extract $whdloadFile.FullName "$imagePath\rdb\$targetDir\$indexDir" --quiet --force
}

Write-Output "Done"