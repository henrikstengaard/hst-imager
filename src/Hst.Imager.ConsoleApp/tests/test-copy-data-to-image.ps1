# Test copy data to image
# -----------------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2026-02-10
#
# A powershell script to test copying many files to an amiga hard disk image file and
# measure elapsed time with and without cache using Hst Imager console.
# This is used to compare the performance of copying files to an amiga hard disk.


# get hst imager path
function GetHstImagerPath($path)
{
    # hst imager path
    $hstImagerPath = Join-Path $path -ChildPath "hst.imager.exe"

    # use hst imager development app, if present
    $hstImagerDevPath = Join-Path $path -ChildPath 'Hst.Imager.ConsoleApp.exe'
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

    return $hstImagerPath
}

# get current directory
$currentDir = Get-Location

# test data directory path
$testDataDir = Join-Path -Path $currentDir -ChildPath "test-data"

# generate test data if it doesn't exist
if (!(Test-Path -Path $testDataDir)) {
    & (Join-Path -Path $currentDir -ChildPath "generate-test-data.ps1")
}

# get hst imager path
$hstImagerPath = GetHstImagerPath $currentDir

# disable cache
& $hstImagerPath settings update --use-cache false

# create blank 1gb image and format rdb pds3
& $hstImagerPath blank test-copy.vhd 1gb
& $hstImagerPath format test-copy.vhd rdb pds3

# copy test data to image and measure elapsed time without cache
$startTime = Get-Date
& $hstImagerPath fs copy test-data test-copy.vhd\rdb\1 -r
$endTime = Get-Date
$noCacheElapsed = $endTime - $startTime

# enable cache
& $hstImagerPath settings update --use-cache true
& $hstImagerPath settings update --cache-type disk

# create blank 1gb image and format rdb pds3
& $hstImagerPath blank test-copy.vhd 1gb
& $hstImagerPath format test-copy.vhd rdb pds3

# copy test data to image and measure elapsed time with disk cache
$startTime = Get-Date
& $hstImagerPath fs copy test-data test-copy.vhd\rdb\1 -r
$endTime = Get-Date
$cacheElapsed = $endTime - $startTime

# print results
Write-Host "No cache: $($noCacheElapsed.TotalSeconds) seconds"
Write-Host "Disk cache: $($cacheElapsed.TotalSeconds) seconds"