# WHDLoad to HDF
# --------------
#
# Author: Henrik Noerfjand Stengaard
# Date:   2023-04-04
#
# A powershell script to convert a WHDLoad .lha file to an amiga harddisk file using Hst Imager console.

trap {
	Write-Error "Exception occured: $($_.Exception)"
	exit 1
}

# add winform assembly for dialogs
Add-Type -AssemblyName System.Windows.Forms

# confirm dialog
function ConfirmDialog($title, $message, $icon = 'Asterisk')
{
	$result = [System.Windows.Forms.MessageBox]::Show($message, $title, [System.Windows.Forms.MessageBoxButtons]::OKCancel, $icon)

	if($result -eq "OK")
	{
		return $true
	}

	return $false
}

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

function SelectAmigaOsAdfFile($title, $path)
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

	# copy adf file
	Copy-Item $adfFile $path
}

# paths
$currentPath = (Get-Location).Path
$scriptPath = Split-Path -Parent $PSCommandPath
$hstImagerPath = Join-Path $currentPath -ChildPath 'hst.imager'

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

# download skick lha from animet, if not found
$skickLhaPath = Join-Path $scriptPath -ChildPath "skick346.lha"
if (!(Test-Path $skickLhaPath))
{
	$url = "https://aminet.net/util/boot/skick346.lha"
	Write-Output ("Downloading url '{0}'" -f $url)
	Invoke-WebRequest -Uri $url -OutFile $skickLhaPath
}

# download whdload usr lha from animet, if not found
$whdloadUsrLhaPath = Join-Path $scriptPath -ChildPath "WHDLoad_usr.lha"
if (!(Test-Path $whdloadUsrLhaPath))
{
	$url = "https://whdload.de/whdload/WHDLoad_usr.lha"
	Write-Output ("Downloading url '{0}'" -f $url)
	Invoke-WebRequest -Uri $url -OutFile $whdloadUsrLhaPath
}

# select and copy amiga os workbench adf, if not present
$amigaOsWorkbenchAdfPath = Join-Path $scriptPath -ChildPath "amiga-os-workbench.adf"
if (!(Test-Path $amigaOsWorkbenchAdfPath))
{
	SelectAmigaOsAdfFile "Select Amiga OS Workbench adf file" $amigaOsWorkbenchAdfPath
}

# select and copy amiga os install adf, if not present
$amigaOsInstallAdfPath = Join-Path $scriptPath -ChildPath "amiga-os-install.adf"
if (!(Test-Path $amigaOsInstallAdfPath))
{
	SelectAmigaOsAdfFile "Select Amiga OS Install adf file" $amigaOsInstallAdfPath
}

# select and copy amiga 500 kickstart 1.3 rom, if not present
$kickstart13A500RomPath = Join-Path $scriptPath -ChildPath "kick34005.A500"
if (!(Test-Path $kickstart13A500RomPath))
{
    $romPath = ${Env:AMIGAFOREVERDATA}
    if ($romPath)
    {
        $romPath = Join-Path $romPath -ChildPath "Shared\rom"
    }
    else
    {
        $romPath = ${Env:USERPROFILE}
    }

    $romFile = OpenFileDialog "Select Amiga 500 Kickstart 1.3 rom file" $romPath "Rom Files|*.rom|All Files|*.*"

    if (!$romFile -or $romFile -eq '')
    {
        throw "Rom file not selected"
    }

    # copy rom file to whdload kickstart naming convention
    Copy-Item $romFile $kickstart13A500RomPath

    # copy rom key, if present
    $romKey = Join-Path (Split-Path $romFile -Parent) -ChildPath "rom.key" 
    if (Test-Path $romKey)
    {
        Copy-Item $romKey $scriptPath
    }
}

# show select whdload lha file open file dialog
$whdloadLhaPath = OpenFileDialog "Select WHDLoad lha file" $romPath "Lha Files|*.lha|All Files|*.*"
if (!$whdloadLhaPath -or $whdloadLhaPath -eq '')
{
    throw "WHDLoad lha file not selected"
}

# get whdload lha entries
$whdloadLhaEntries = @()
$whdloadLhaEntries += (& $hstImagerPath fs dir "$whdloadLhaPath" --recursive --format json | ConvertFrom-Json).Entries

# find whdload slaves in entires
$whdloadSlavePaths = @()
$whdloadSlavePaths += $whdloadLhaEntries.Where{ $_.Name -match '\.slave$' }.Name

# error, if whdload lha file doesn't contain any .slave files
if ($whdloadSlavePaths.Count -eq 0)
{
	Write-Error ("No WHDLoad slave files found in '{0}'" -f $whdloadLhaPath)
	exit 1
}

# calculate disk size
$diskSize = 0
foreach ($whdloadLhaEntry in $whdloadLhaEntries)
{	
	$diskSize += $whdloadLhaEntry.Size
}

# add 10mb extra for amiga os, kickstart and whdload files
$diskSize += 10 * 1024 * 1024

# show use pfs3 question dialog 
$usePfs3 = QuestionDialog 'Use PFS3 file system' "Do you want to use PFS3 file system?`r`n`r`nIf No then DOS3 file system is used and will be imported`r`nfrom Amiga OS install disk."

# get image path based on selected whdload lha
$imagePath = Join-Path $currentPath -ChildPath ("{0}.vhd" -f ([System.IO.Path]::GetFileNameWithoutExtension($whdloadLhaPath)))
Write-Output ("Creating image file '{0}'" -f $imagePath)

# create blank image of calculated disk size
& $hstImagerPath blank "$imagePath" $diskSize

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
	& $hstImagerPath rdb fs import "$imagePath" $amigaOsInstallAdfPath --dos-type DOS3 --name FastFileSystem

	# add rdb partition of entire disk with device name "DH0" and set bootable
	& $hstImagerPath rdb part add "$imagePath" DH0 DOS3 * --bootable
}

# format rdb partition number 1 with volume name "WHDLoad"
& $hstImagerPath rdb part format "$imagePath" 1 WHDLoad

# extract amiga os install adf to image file
& $hstImagerPath fs extract $amigaOsInstallAdfPath "$imagePath\rdb\dh0"

# extract amiga os workbench adf to image file
& $hstImagerPath fs extract $amigaOsWorkbenchAdfPath "$imagePath\rdb\dh0"

# extract whdload lha to image file
& $hstImagerPath fs extract "$whdloadLhaPath" "$imagePath\rdb\dh0\WHDLoad" --recursive

# copy kickstart 1.3 to image file
& $hstImagerPath fs copy $kickstart13A500RomPath "$imagePath\rdb\dh0\Devs\Kickstarts"

# copy rom.key to image file, if present
if (Test-Path "rom.key")
{
	& $hstImagerPath fs copy "rom.key" "$imagePath\rdb\dh0\Devs\Kickstarts"
}

# extract soft-kicker lha to image file
& $hstImagerPath fs extract (Join-Path $skickLhaPath -ChildPath "Kickstarts") "$imagePath\rdb\dh0\Devs\Kickstarts"

# extract whdload lha to image file
& $hstImagerPath fs extract (Join-Path $whdloadUsrLhaPath -ChildPath "WHDLoad\C") "$imagePath\rdb\dh0\C"
& $hstImagerPath fs extract (Join-Path $whdloadUsrLhaPath -ChildPath "WHDLoad\S") "$imagePath\rdb\dh0\S"

# create startup sequence
$startupSequenceLines = @(
	"C:SetPatch QUIET",
	"C:Version >NIL:",
	"FailAt 21",
	"C:MakeDir RAM:T RAM:Clipboards RAM:ENV RAM:ENV/Sys",
	"C:Assign T: RAM:T"
)

# add whdload slave start to startup sequence
if ($whdloadSlavePaths.Count -eq 1)
{
	$whdloadSlavePath = $whdloadSlavePaths[0]
	$startupSequenceLines += @(
		("cd ""{0}""" -f ((Split-Path -Parent "WHDLoad/$whdloadSlavePath") -replace '\\', '/')),
		("WHDLoad ""{0}"" PRELOAD" -f [System.IO.Path]::GetFileName($whdloadSlavePath))
	)
}
else
{
	$options = @()
	$options += $whdloadSlavePaths | ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) }
	$startupSequenceLines += ("set slave ``RequestChoice ""Start WHDLoad slave"" ""Select WHDLoad slave to start?"" ""{0}""``" -f ($options -join '|'))

	$option = 1
	foreach ($whdloadSlavePath in $whdloadSlavePaths)
	{
		if ($option -eq $whdloadSlavePaths.Count)
		{
			$option = 0
		}

		$startupSequenceLines += @(
			"IF ""`$slave"" EQ $option VAL ",
			("  cd ""{0}""" -f ((Split-Path -Parent "WHDLoad/$whdloadSlavePath") -replace '\\', '/'))
			("  WHDLoad ""{0}"" PRELOAD" -f [System.IO.Path]::GetFileName($whdloadSlavePath))
			"  SKIP end"
			"ENDIF"
		)

		$option++
	}

	$startupSequenceLines += "LAB end"
}

# write startup sequence
$startupSequence = $startupSequenceLines -join "`n"
$startupSequencePath = (Join-Path $scriptPath -ChildPath "Startup-Sequence")
[System.IO.File]::WriteAllText($startupSequencePath, $startupSequence, [System.Text.Encoding]::GetEncoding('iso-8859-1'))

# copy startup sequence to image file
& $hstImagerPath fs copy $startupSequencePath "$imagePath\rdb\dh0\S"

Write-Output "Done"