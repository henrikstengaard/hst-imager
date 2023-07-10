# Shared
# ------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2023-07-10
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

function SelectAdfFile($title)
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

function SelectRomFile($title)
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

    $romFile = OpenFileDialog $title $romPath "Rom Files|*.rom|All Files|*.*"

    if (!$romFile -or $romFile -eq '')
    {
        Write-Error "Rom file not selected"
        exit 1
    }

    return $romFile
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

# get adf files
function GetAdfFiles($adfFiles, $outputPath)
{
    if (!(Test-Path $outputPath))
    {
        mkdir $outputPath | Out-Null
    }

    # set src path to output path
    $srcPath = $outputPath
    
    foreach ($adfFile in $adfFiles)
    {
        $destAdfExists = $false

        while (!$destAdfExists)
        {
            $destAdfPath = Join-Path $outputPath -ChildPath $adfFile.Filename
            $destAdfExists = Test-Path $destAdfPath

            # skip, if adf file exists in output path
            if ($destAdfExists)
            {
                break
            }

            $adfPath = Join-Path $srcPath -ChildPath $adfFile.Filename

            # skip, if adf path exist src adf path
            if (Test-Path $adfPath)
            {
                # copy adf path to dest adf path
                Copy-Item $adfPath $destAdfPath -Force

                # remove readonly
                if ((Get-ChildItem -Path $destAdfPath).IsReadOnly)
                {
                    Set-ItemProperty $destAdfPath -name IsReadOnly -value $false
                }

                break
            }

            # select adf file
            $adfPath = SelectAdfFile ("Select {0} adf file" -f $adfFile.Name)
            if (!(Test-Path $adfPath))
            {
                Write-Error ("Error: {0} adf file \'{1}\' not found" -f $adfFile.Name, $adfPath)
                continue
            }

            # set src path to path with adf file
            $srcPath = Split-Path $adfPath -Parent

            # copy adf path to dest adf path
            Copy-Item $adfPath $destAdfPath -Force

            # remove readonly
            if ((Get-ChildItem -Path $destAdfPath).IsReadOnly)
            {
                Set-ItemProperty $destAdfPath -name IsReadOnly -value $false
            }
            
            break
        }
    }
}

# get rom files
function GetRomFiles($romFiles, $outputPath)
{
    # create output path, if not exist
    if (!(Test-Path $outputPath))
    {
        mkdir $outputPath | Out-Null
    }

    # set src path to output path
    $srcPath = $outputPath
    
    # set dest rom key
    $destRomKey = Join-Path $outputPath -ChildPath "rom.key"
    
    foreach ($romFile in $romFiles)
    {
        $destRomExists = $false

        while (!$destRomExists)
        {
            $destRomPath = Join-Path $outputPath -ChildPath $romFile.DestFilename
            $destRomExists = Test-Path $destRomPath

            # skip, if rom file exists in output path
            if ($destRomExists)
            {
                break
            }

            $romPath = Join-Path $srcPath -ChildPath $romFile.SrcFilename

            # skip, if rom path exist src path
            if (Test-Path $romPath)
            {
                # copy rom path to dest rom path
                Copy-Item $romPath $destRomPath -Force

                # remove readonly
                if ((Get-ChildItem -Path $destRomPath).IsReadOnly)
                {
                    Set-ItemProperty $destRomPath -name IsReadOnly -value $false
                }

                break
            }

            # select rom file
            $romPath = SelectRomFile ("Select {0} rom file" -f $romFile.Name)
            if (!(Test-Path $romPath))
            {
                Write-Error ("Error: {0} rom file \'{1}\' not found" -f $romFile.Name, $romPath)
                continue
            }

            # set src path to path with rom file
            $srcPath = Split-Path $romPath -Parent

            # copy rom path to dest rom path
            Copy-Item $romPath $destRomPath -Force

            # remove readonly
            if ((Get-ChildItem -Path $destRomPath).IsReadOnly)
            {
                Set-ItemProperty $destRomPath -name IsReadOnly -value $false
            }

            break
        }

        # copy rom key, if present
        $romKey = Join-Path $srcPath -ChildPath "rom.key"
        if (!(Test-Path $destRomKey) -and (Test-Path $romKey))
        {
            Copy-Item $romKey $outputPath
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

    $initialPath = ${Env:AMIGAFOREVERDATA}
    if ($initialPath)
    {
        $initialPath = Join-Path $initialPath -ChildPath "Shared\adf"
    }
    else
    {
        $initialPath = (Get-Location).Path
    }
    
    $selectedAdfPath = OpenFileDialog $title $initialPath "Adf Files|*.adf|All Files|*.*"

    # throw error, if new image directory path is null
    if ($null -eq $selectedAdfPath)
    {
        throw "No adf selected!"
    }

    # copy adf path
    Copy-Item $selectedAdfPath $adfPath -Force

    # remove readonly
    if ((Get-ChildItem -Path $adfPath).IsReadOnly)
    {
        Set-ItemProperty $adfPath -name IsReadOnly -value $false
    }

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

    $amigaOsWorkbenchAdfPath = Join-Path $path -ChildPath "amigaos-3.x-workbench.adf"
    if (Test-Path $amigaOsWorkbenchAdfPath)
    {
        return $amigaOsWorkbenchAdfPath
    }

    return GetAmigaOsAdfPath "Select Amiga OS 3.1+ Workbench adf" $amigaOsWorkbenchAdfPath
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
    
    $amigaOsInstallAdfPath = Join-Path $path -ChildPath "amigaos-3.x-install.adf"
    if (Test-Path $amigaOsInstallAdfPath)
    {
        return $amigaOsInstallAdfPath
    }

    return GetAmigaOsAdfPath "Select Amiga OS 3.1+ Install adf" $amigaOsInstallAdfPath
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

function InstallKickstartRoms($hstImagerPath, $imagePath)
{
    # kickstart rom files
    $kickstartRomFiles = @(
        @{
            'SrcFilename' = 'amiga-os-130.rom';
            'DestFilename' = 'kick34005.A500';
            'Name' = 'Amiga 500 Kickstart 1.3'
        },
        @{
            'SrcFilename' = 'amiga-os-120.rom';
            'DestFilename' = 'kick33180.A500';
            'Name' = 'Amiga 500 Kickstart 1.2'
        },
        @{
            'SrcFilename' = 'amiga-os-310-a600.rom';
            'DestFilename' = 'kick40063.A600';
            'Name' = 'Amiga 600 Kickstart 3.1'
        },
        @{
            'SrcFilename' = 'amiga-os-310-a1200.rom';
            'DestFilename' = 'kick40068.A1200';
            'Name' = 'Amiga 1200 Kickstart 3.1'
        },
        @{
            'SrcFilename' = 'amiga-os-310-a4000.rom';
            'DestFilename' = 'kick40068.A4000';
            'Name' = 'Amiga 4000 Kickstart 3.1'
        }
    )

    $imageDir = Split-Path $imagePath -Parent

    # get rom files copied to image dir
    GetRomFiles $kickstartRomFiles $imageDir
    
    # kickstart rom paths
    $kickstart12A500RomPath = Join-Path $imageDir -ChildPath "kick33180.A500"
    $kickstart13A500RomPath = Join-Path $imageDir -ChildPath "kick34005.A500"
    $kickstart31A600RomPath = Join-Path $imageDir -ChildPath "kick40063.A600"
    $kickstart31A1200RomPath = Join-Path $imageDir -ChildPath "kick40068.A1200"
    $kickstart31A4000RomPath = Join-Path $imageDir -ChildPath "kick40068.A4000"
    $romKeyPath = Join-Path $imageDir -ChildPath "rom.key"
    
    # copy kickstart roms to image file
    & $hstImagerPath fs copy $kickstart12A500RomPath "$imagePath\rdb\dh0\Devs\Kickstarts"
    & $hstImagerPath fs copy $kickstart13A500RomPath "$imagePath\rdb\dh0\Devs\Kickstarts"
    & $hstImagerPath fs copy $kickstart31A600RomPath "$imagePath\rdb\dh0\Devs\Kickstarts"
    & $hstImagerPath fs copy $kickstart31A1200RomPath "$imagePath\rdb\dh0\Devs\Kickstarts"
    & $hstImagerPath fs copy $kickstart31A4000RomPath "$imagePath\rdb\dh0\Devs\Kickstarts"

    # copy rom.key to image file, if present
    if (Test-Path $romKeyPath)
    {
        & $hstImagerPath fs copy $romKeyPath "$imagePath\rdb\dh0\Devs\Kickstarts"
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
    # install kickstart roms
    InstallKickstartRoms $hstImagerPath $imagePath
    
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
