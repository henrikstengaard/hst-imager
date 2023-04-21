# Install minimal WHDLoad
# -----------------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2023-04-21
#
# A powershell script to install minimal WHDLoad requires files to an amiga harddisk file using Hst Imager console.

Param(
	[Parameter(Mandatory=$true)]
	[string]$imagePath,
	[Parameter(Mandatory=$false)]
	[switch]$noAmigaOs
)

trap {
	Write-Error "Exception occured: $($_.Exception)"
	exit 1
}

# add winform assembly for dialogs
Add-Type -AssemblyName System.Windows.Forms

# show confirm dialog using winforms
function ConfirmDialog($title, $message, $icon = 'Asterisk')
{
	$result = [System.Windows.Forms.MessageBox]::Show($message, $title, [System.Windows.Forms.MessageBoxButtons]::OKCancel, $icon)

	if($result -eq "OK")
	{
		return $true
	}

	return $false
}

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

# error, if image path is not found
if (!(Test-Path $imagePath))
{
	Write-Error ("Image path '{0}' not found" -f $imagePath)
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
if (!$noAmigaOs -and !(Test-Path $amigaOsWorkbenchAdfPath))
{
	SelectAmigaOsAdfFile "Select Amiga OS Workbench adf file" $amigaOsWorkbenchAdfPath
}

# select and copy amiga os install adf, if not present
$amigaOsInstallAdfPath = Join-Path $scriptPath -ChildPath "amiga-os-install.adf"
if (!$noAmigaOs -and !(Test-Path $amigaOsInstallAdfPath))
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

# skip install amiga os, if no amiga os is true
if (!$noAmigaOs)
{
	# extract amiga os install adf to image file
	& $hstImagerPath fs extract $amigaOsInstallAdfPath "$imagePath\rdb\dh0"

	# extract amiga os workbench adf to image file
	& $hstImagerPath fs extract $amigaOsWorkbenchAdfPath "$imagePath\rdb\dh0"
}

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

Write-Output "Done"