# Shared
# ------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2023-06-04
#
# A powershell module with shared functions for example scripts.

# add winform assembly for dialogs
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName Microsoft.VisualBasic

# show question dialog using winforms
Function QuestionDialog($title, $message, $icon = 'Question')
{
    $result = [System.Windows.Forms.MessageBox]::Show($message, $title, [System.Windows.Forms.MessageBoxButtons]::YesNo, $icon)

    if($result -eq "YES")
    {
        return $true
    }

    return $false
}

# show open file dialog using winforms
Function OpenFileDialog($title, $directory, $filter)
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
Function FolderBrowserDialog($title, $directory, $showNewFolderButton)
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

# read text lines for amiga
function ReadTextLinesForAmiga($path)
{
    return [System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::GetEncoding('iso-8859-1'))
}

# write text lines for amiga
function WriteTextLinesForAmiga($path, $lines)
{
    $text = $lines -join "`n"
    [System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::GetEncoding('iso-8859-1'))
}

# get amigaos files
function GetAmigaOsFiles($files, $path)
{
    if (!(Test-Path $path))
    {
        mkdir $path | Out-Null
    }
    
    $srcAdfPath = $path
    foreach ($file in $files)
    {
        $destAdfExists = $false

        while (!$destAdfExists)
        {
            $destAdfFilename = $file.Filename
            $destAdfPath = Join-Path $path -ChildPath $destAdfFilename
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

                # remove readonly
                Set-ItemProperty $destAdfPath -name IsReadOnly -value $false

                break
            }

            $adfPath = SelectAmigaOsAdfFile ("Select {0} adf file" -f $file.Name)
            $adfExists = Test-Path $adfPath

            if (!$adfExists)
            {
                Write-Error ("Error: {0} adf file \'{1}\' not found" -f $file.Name, $adfPath)
                continue
            }

            $srcAdfPath = Split-Path $adfPath -Parent

            # copy selected adf path
            Copy-Item $adfPath $destAdfPath -Force

            # remove readonly
            Set-ItemProperty $destAdfPath -name IsReadOnly -value $false
            
            break
        }
    }
}

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

# get hst amiga path
function GetHstAmigaPath($path)
{
    # hst amiga path
    $hstAmigaPath = Join-Path $path -ChildPath "hst.amiga.exe"

    # use hst amiga development app, if present
    $hstAmigaDevPath = Join-Path $path -ChildPath 'Hst.Amiga.ConsoleApp.exe'
    if (Test-Path $hstAmigaDevPath)
    {
        $hstAmigaPath = $hstAmigaDevPath
    }

    # error, if hst amiga is not found
    if (!(Test-Path $hstAmigaPath))
    {
        Write-Error ("Hst Amiga file '{0}' not found" -f $hstAmigaPath)
        exit 1
    }

    return $hstAmigaPath
}

function GetAmigaOsAdfPath($title, $adfPath)
{
    if (Test-Path $adfPath)
    {
        return $adfPath
    }

    $currentPath = (Get-Location).Path
    $selectedAdfPath = OpenFileDialog $title $currentPath "Adf Files|*.adf|All Files|*.*"

    # throw error, if new image directory path is null
    if ($null -eq $selectedAdfPath)
    {
        throw "No adf selected!"
    }

    # copy adf path
    Copy-Item $selectedAdfPath $adfPath -Force

    # remove readonly
    Set-ItemProperty $adfPath -name IsReadOnly -value $false

    return $adfPath
}

# get amigaos workbench adf path
function GetAmigaOsWorkbenchAdfPath($path, $useAmigaOs31)
{
    if ($useAmigaOs31)
    {
        # amigaos 3.1 workbench adf path
        $amigaOsWorkbenchAdfPath = Join-Path $path -ChildPath "amiga-os-310-workbench.adf"
        if (Test-Path $amigaOsWorkbenchAdfPath)
        {
            return $amigaOsWorkbenchAdfPath
        }

        return GetAmigaOsAdfPath "Select Amiga OS 3.1 Workbench adf" $amigaOsWorkbenchAdfPath
    }

    $amigaOsWorkbenchAdfPath = Join-Path $path -ChildPath "Workbench3.2.adf"
    if (Test-Path $amigaOsWorkbenchAdfPath)
    {
        return $amigaOsWorkbenchAdfPath
    }

    $amigaOsWorkbenchAdfPath = Join-Path $path -ChildPath "Workbench3_1_4.adf"
    if (Test-Path $amigaOsWorkbenchAdfPath)
    {
        return $amigaOsWorkbenchAdfPath
    }

    $amigaOsWorkbenchAdfPath = Join-Path $path -ChildPath "amigaos-3.1.4-3.2-workbench.adf"
    if (Test-Path $amigaOsWorkbenchAdfPath)
    {
        return $amigaOsWorkbenchAdfPath
    }

    return GetAmigaOsAdfPath "Select Amiga OS 3.1.4, 3.2+ Workbench adf" $amigaOsWorkbenchAdfPath
}

# get amigaos install adf path
function GetAmigaOsInstallAdfPath($path, $useAmigaOs31)
{
    if ($useAmigaOs31)
    {
        # amigaos 3.1 install adf path
        $amigaOsInstallAdfPath = Join-Path $path -ChildPath "amiga-os-310-install.adf"
        if (Test-Path $amigaOsInstallAdfPath)
        {
            return $amigaOsInstallAdfPath
        }

        return GetAmigaOsAdfPath "Select Amiga OS 3.1 Install adf" $amigaOsInstallAdfPath
    }
    
    $amigaOsInstallAdfPath = Join-Path $path -ChildPath "Install3.2.adf"
    if (Test-Path $amigaOsInstallAdfPath)
    {
        return $amigaOsInstallAdfPath
    }

    $amigaOsInstallAdfPath = Join-Path $path -ChildPath "Install3_1_4.adf"
    if (Test-Path $amigaOsInstallAdfPath)
    {
        return $amigaOsInstallAdfPath
    }

    $amigaOsInstallAdfPath = Join-Path $path -ChildPath "amigaos-3.1.4-3.2-install.adf"
    if (Test-Path $amigaOsInstallAdfPath)
    {
        return $amigaOsInstallAdfPath
    }

    return GetAmigaOsAdfPath "Select Amiga OS 3.1.4, 3.2+ Install adf" $amigaOsInstallAdfPath
}

function CreateImage($hstImagerPath, $imagePath, $size)
{
    # show use pfs3 question dialog
    $usePfs3 = QuestionDialog 'Use PFS3 file system' "Do you want to use PFS3 file system?`r`n`r`nIf No then DOS7 file system is used and will be imported`r`nfrom Amiga 3.1.4, 3.2+ install adf disk."
    
    # get amigaos install adf path
    $amigaOsInstallAdfPath = $null
    if (!$usePfs3)
    {
        $amigaOsInstallAdfPath = GetAmigaOsInstallAdfPath (Split-Path -Parent $imagePath)
    }

    Write-Output ("Creating image file '{0}' of size {1}" -f $imagePath, $size)

    # create blank image of size
    & $hstImagerPath blank "$imagePath" "$size"

    # initialize rigid disk block for entire disk
    & $hstImagerPath rdb init "$imagePath"

    if ($usePfs3)
    {
        # add rdb file system pfs3aio with dos type PDS3
        & $hstImagerPath rdb fs add "$imagePath" pfs3aio PDS3

        # add rdb partition of 500mb disk space with device name "DH0" and set bootable
        & $hstImagerPath rdb part add "$imagePath" DH0 PDS3 500mb --bootable

        # add rdb partition of remaining disk space with device name "DH1"
        & $hstImagerPath rdb part add "$imagePath" DH1 PDS3 *
    }
    else
    {
        # add rdb file system fast file system with dos type DOS7 imported from amiga os install adf
        & $hstImagerPath rdb fs import "$imagePath" "$amigaOsInstallAdfPath" --dos-type DOS7 --name FastFileSystem

        # add rdb partition of entire disk with device name "DH0" and set bootable
        & $hstImagerPath rdb part add "$imagePath" DH0 DOS7 500mb --bootable

        # add rdb partition of remaining disk space with device name "DH1"
        & $hstImagerPath rdb part add "$imagePath" DH1 DOS7 *
    }
    
    # format rdb partition number 1 with volume name "Workbench"
    & $hstImagerPath rdb part format "$imagePath" 1 Workbench

    # format rdb partition number 2 with volume name "Work"
    & $hstImagerPath rdb part format "$imagePath" 2 Work
}

function InstallMinimalAmigaOs($hstImagerPath, $imagePath, $useAmigaOs31)
{
    $imageDir = Split-Path $imagePath -Parent

    # get amigaos workbench and install adf
    $amigaOsWorkbenchAdfPath = GetAmigaOsWorkbenchAdfPath $imageDir $useAmigaOs31
    $amigaOsInstallAdfPath = GetAmigaOsInstallAdfPath $imageDir $useAmigaOs31

    # extract amiga os install adf to image file
    & $hstImagerPath fs extract $amigaOsInstallAdfPath "$imagePath\rdb\dh0"

    # extract amiga os workbench adf to image file
    & $hstImagerPath fs extract $amigaOsWorkbenchAdfPath "$imagePath\rdb\dh0"
}

function InstallKickstart13Rom($hstImagerPath, $imagePath)
{
    $imageDir = Split-Path $imagePath -Parent
    
    # select and copy amiga 500 kickstart 1.3 rom, if not present
    $kickstart13A500RomPath = Join-Path $imageDir -ChildPath "kick34005.A500"
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

    # copy kickstart 1.3 to image file
    & $hstImagerPath fs copy $kickstart13A500RomPath "$imagePath\rdb\dh0\Devs\Kickstarts"

    # copy rom.key to image file, if present
    if (Test-Path "rom.key")
    {
        & $hstImagerPath fs copy "rom.key" "$imagePath\rdb\dh0\Devs\Kickstarts"
    }
}

# get skick lha path, download lha if not found
function GetSkickLhaPath($downloadPath)
{
    $skickLhaPath = Join-Path $downloadPath -ChildPath "skick346.lha"
    if (Test-Path $skickLhaPath)
    {
        return $skickLhaPath
    }
    $url = "https://aminet.net/util/boot/skick346.lha"
    Write-Host ("Downloading url '{0}'" -f $url)
    Invoke-WebRequest -Uri $url -OutFile $skickLhaPath
    return $skickLhaPath
}

# get whdload usr lha path, download lha if not found
function GetWhdloadLhaPath($downloadPath)
{
    $whdloadUsrLhaPath = Join-Path $downloadPath -ChildPath "WHDLoad_usr.lha"
    if (Test-Path $whdloadUsrLhaPath)
    {
        return $whdloadUsrLhaPath
    }
    $url = "https://whdload.de/whdload/WHDLoad_usr.lha"
    Write-Host ("Downloading url '{0}'" -f $url)
    Invoke-WebRequest -Uri $url -OutFile $whdloadUsrLhaPath
    return $whdloadUsrLhaPath
}

# get iconlib lha path, download lha if not found
function GetIconLibLhaPath($downloadPath)
{
    $iconLibUsrLhaPath = Join-Path $downloadPath -ChildPath "IconLib_46.4.lha"
    if (Test-Path $iconLibUsrLhaPath)
    {
        return $iconLibUsrLhaPath
    }
    $url = "https://aminet.net/util/libs/IconLib_46.4.lha"
    Write-Host ("Downloading url '{0}'" -f $url)
    Invoke-WebRequest -Uri $url -OutFile $iconLibUsrLhaPath
    return $iconLibUsrLhaPath
}

# install minimal whdload
function InstallMinimalWhdload($hstImagerPath, $imagePath)
{
    $imageDir = Split-Path $imagePath -Parent

    $sKickLhaPath = GetSkickLhaPath $imageDir
    $whdloadUsrLhaPath = GetWhdloadLhaPath $imageDir
    $iconLibLhaPath = GetIconLibLhaPath $imageDir

    # extract soft-kicker lha to image file
    & $hstImagerPath fs extract "$sKickLhaPath\Kickstarts" "$imagePath\rdb\dh0\Devs\Kickstarts"

    # extract whdload lha to image file
    & $hstImagerPath fs extract "$whdloadUsrLhaPath\WHDLoad\C" "$imagePath\rdb\dh0\C"
    & $hstImagerPath fs extract "$whdloadUsrLhaPath\WHDLoad\S" "$imagePath\rdb\dh0\S"

    # extract iconlib lha to image file
    & $hstImagerPath fs extract "$iconLibLhaPath\IconLib_46.4\Libs\68000\icon.library" "$imagePath\rdb\dh0\Libs"
    & $hstImagerPath fs extract "$iconLibLhaPath\IconLib_46.4\ThirdParty\RemLib\RemLib" "$imagePath\rdb\dh0\C"
    & $hstImagerPath fs extract "$iconLibLhaPath\IconLib_46.4\ThirdParty\LoadResident\LoadResident" "$imagePath\rdb\dh0\C"

    # extract image file startup sequence
    & $hstImagerPath fs copy "$imagePath\rdb\dh0\S\Startup-Sequence" $imageDir

    # read startup sequence
    $startupSequencePath = Join-Path $imageDir -ChildPath "Startup-Sequence"
    $startupSequenceLines = ReadTextLinesForAmiga $startupSequencePath

    # create remlib lines for icon library
    $remLibLines = @(
        "If EXISTS Libs:icon.library",
        "  RemLib >NIL: icon.library",
        "  If EXISTS Libs:workbench.library",
        "    RemLib >NIL: workbench.library",
        "  EndIf",
        "EndIf",
        ""
    )

    # add remlib lines at beginning of startup sequence
    $startupSequenceLines = $remLibLines + $startupSequenceLines

    # write startup sequence
    WriteTextLinesForAmiga $startupSequencePath $startupSequenceLines

    # copy startup sequence to image file
    & $hstImagerPath fs copy $startupSequencePath "$imagePath\rdb\dh0\S"
}
