# Shared
# ------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2023-05-14
#
# A python script with shared functions for example scripts.

"""Shared"""

import os
import re
import stat
import shutil
import subprocess
import sys
import codecs
import unicodedata
from urllib.request import urlretrieve

# confirm
def confirm(message):
    return re.search(r'^(|y|yes)$', input(message), re.I)

# run command
def run_command(commands):
    """Run command"""

    # process to run commands
    process = subprocess.run(commands)

    # return, if return code is not 0
    if process.returncode:
        print(stderr)
        exit(1)

# run command and capture output
def run_command_capture_output(commands):
    """Run command"""

    # process to run commands
    process = subprocess.Popen(commands, bufsize=-1, text=True,
                               stdout=subprocess.PIPE, stderr=subprocess.PIPE)

    # get stdout and stderr from process
    (stdout, stderr) = process.communicate()

    # return, if return code is not 0
    if process.returncode:
        print(stderr)
        exit(1)

    return stdout

# get hst imager path
def get_hst_imager_path(path):
    # use hst imager macos/linux app by default
    hst_imager_path = os.path.join(path, 'hst.imager')

    # use hst imager windows exe app, if present
    hst_imager_dev = os.path.join(path, 'hst.imager.exe')
    if os.path.isfile(hst_imager_dev):
        hst_imager_path = hst_imager_dev

    # use hst imager macos/linux development app, if present
    hst_imager_dev = os.path.join(path, 'Hst.Imager.ConsoleApp')
    if os.path.isfile(hst_imager_dev):
        hst_imager_path = hst_imager_dev

    # use hst imager windows development app, if present
    hst_imager_dev = os.path.join(path, 'Hst.Imager.ConsoleApp.exe')
    if os.path.isfile(hst_imager_dev):
        hst_imager_path = hst_imager_dev

    # error, if hst imager is not found
    if not os.path.isfile(hst_imager_path):
        print('Error: Hst Imager file \'{0}\' not found'.format(hst_imager_path))
        exit(1)

    return hst_imager_path

# get hst amiga path
def get_hst_amiga_path(path):
    # use hst amiga macos/linux app by default
    hst_amiga_path = os.path.join(path, 'hst.amiga')
    
    # use hst amiga windows exe app, if present
    hst_amiga_dev = os.path.join(path, 'hst.amiga.exe')
    if os.path.isfile(hst_amiga_dev):
        hst_amiga_path = hst_amiga_dev

    # use hst imager macos/linux development app, if present
    hst_amiga_dev = os.path.join(path, 'Hst.Amiga.ConsoleApp')
    if os.path.isfile(hst_amiga_dev):
        hst_amiga_path = hst_amiga_dev

    # use hst imager windows development app, if present
    hst_amiga_dev = os.path.join(path, 'Hst.Amiga.ConsoleApp.exe')
    if os.path.isfile(hst_amiga_dev):
        hst_amiga_path = hst_amiga_dev

    # error, if hst amiga is not found
    if not os.path.isfile(hst_amiga_path):
        print('Error: Hst Amiga file \'{0}\' not found.'.format(hst_amiga_path))
        exit(1)

    return hst_amiga_path

# read text lines for amiga
def read_text_lines_for_amiga(path):
    """Read Text Lines for Amiga"""
    lines = []
    with codecs.open(path, "r", "iso-8859-1") as file:
        while line := file.readline():
            lines.append(line.rstrip())
    return lines

# write text lines for amiga
def write_text_lines_for_amiga(path, lines):
    """Write Text Lines for Amiga"""
    with codecs.open(path, "w", "iso-8859-1") as file:
        for line in lines:
            file.write(unicodedata.normalize('NFC', line)+"\n")

# enter amigaos adf path
def enter_amigaos_adf_path(title, path):
    adf_file = os.path.abspath(input("{0}: ".format(title)))

    if not os.path.isfile(adf_file):
        print('Error: Adf file \'{0}\' doesn\'t exist'.format(adf_file))
        exit(1)

    # copy adf file
    shutil.copyfile(
        adf_file,
        path)
    os.chmod(path, os.stat(path).st_mode | stat.S_IREAD | stat.S_IWRITE | stat.S_IEXEC)

def get_amigaos_adf_path(title, path):
    if os.path.isfile(path):
        return
    enter_amigaos_adf_path(title, path)

# enter kickstart rom path
def enter_kickstart_rom_path(title, path):
    rom_file = os.path.abspath(input("{0}: ".format(title)))

    if not os.path.isfile(rom_file):
        print('Error: Rom file \'{0}\' doesn\'t exist'.format(rom_file))
        exit(1)

    # copy rom file
    shutil.copyfile(
        rom_file,
        path)
    os.chmod(path, os.stat(path).st_mode | stat.S_IREAD | stat.S_IWRITE | stat.S_IEXEC)
    
    # copy rom key, if present
    rom_key_file = os.path.join(os.path.dirname(rom_file), "rom.key")
    if os.path.exists(rom_key_file):
        dest_rom_key_file = os.path.join(os.path.dirname(path), "rom.key")
        shutil.copyfile(
            rom_key_file,
            dest_rom_key_file)
        os.chmod(dest_rom_key_file, os.stat(dest_rom_key_file).st_mode | stat.S_IREAD | stat.S_IWRITE | stat.S_IEXEC)

# create image
def create_image(hst_imager_path, image_path, size):
    # show use pfs3 confirm dialog 
    use_pfs3 = re.search(r'^(|y|yes)$', input("Use PFS3 file system? (enter = yes, no = DOS7):"), re.I)

    amigaos_install_adf_path = os.path.join(os.path.dirname(image_path), "amigaos-314-32-install.adf")
    if not use_pfs3:
        print('Using DOS7 requires Fast File System from Amiga OS 3.1.4, 3.2+ install adf')
        get_amigaos_adf_path("Enter Amiga OS 3.1.4, 3.2+ install adf path", amigaos_install_adf_path)

    print('Creating image file \'{0}\' of size {1}'.format(image_path, size))
    
    # create blank image of calculated disk size
    run_command([hst_imager_path, 'blank', image_path, size, '--compatible'])
    
    # initialize rigid disk block for entire disk
    run_command([hst_imager_path, 'rdb', 'init', image_path])
    
    if use_pfs3:
        # add rdb file system pfs3aio with dos type PDS3
        run_command([hst_imager_path, 'rdb', 'fs', 'add', image_path, 'pfs3aio', 'PDS3'])

        # add rdb partition of 500mb disk space with device name "DH0" and set bootable
        run_command([hst_imager_path, 'rdb', 'part', 'add', image_path, 'DH0', 'PDS3', '500mb', '--bootable'])
        
        # add rdb partition of remaining disk space with device name "DH1"
        run_command([hst_imager_path, 'rdb', 'part', 'add', image_path, 'DH1', 'PDS3', '*'])
    else:
        # add rdb file system fast file system with dos type DOS7 imported from amigaos install adf
        run_command([hst_imager_path, 'rdb', 'fs', 'import', image_path, amigaos_install_adf_path, '--dos-type', 'DOS7', '--name', 'FastFileSystem'])

        # add rdb partition of 500mb disk space with device name "DH0" and set bootable
        run_command([hst_imager_path, 'rdb', 'part', 'add', image_path, 'DH0', 'DOS7', '500mb', '--bootable'])

        # add rdb partition of remaining disk space with device name "DH1"
        run_command([hst_imager_path, 'rdb', 'part', 'add', image_path, 'DH1', 'DOS7', '*'])

    # format rdb partition number 1 with volume name "Workbench"
    run_command([hst_imager_path, 'rdb', 'part', 'format', image_path, '1', 'Workbench'])
    
    # format rdb partition number 2 with volume name "Work"
    run_command([hst_imager_path, 'rdb', 'part', 'format', image_path, '2', 'Work'])

# get skick lha path, download lha if not found
def get_skick_lha_path(download_path):
    skick_lha_path = os.path.join(download_path, "skick346.lha")
    if os.path.isfile(skick_lha_path):
        return skick_lha_path
    url = 'https://aminet.net/util/boot/skick346.lha'
    print('Downloading url \'{0}\''.format(url))
    urlretrieve(url, skick_lha_path)
    return skick_lha_path

# get whdload lha path, download lha if not found
def get_whdload_lha_path(download_path):
    whdload_usr_lha_path = os.path.join(download_path, "WHDLoad_usr.lha")
    if os.path.isfile(whdload_usr_lha_path):
        return whdload_usr_lha_path
    url = 'https://whdload.de/whdload/WHDLoad_usr.lha'
    print('Downloading url \'{0}\''.format(url))
    urlretrieve(url, whdload_usr_lha_path)
    return whdload_usr_lha_path

# get iconlib lha path, download lha if not found
def get_iconlib_lha_path(download_path):
    iconlib_lha_path = os.path.join(download_path, "IconLib_46.4.lha")
    if os.path.isfile(iconlib_lha_path):
        return iconlib_lha_path
    url = 'http://aminet.net/util/libs/IconLib_46.4.lha'
    print('Downloading url \'{0}\''.format(url))
    urlretrieve(url, iconlib_lha_path)
    return iconlib_lha_path

def get_amigaos_workbench_adf_path(path):
    # amigaos 3.1 workbench adf path
    amigaos_workbench_adf_path = os.path.join(path, "amiga-os-310-workbench.adf")

    # return amigaos 3.1 workbench adf path, it exists and confirm use amigaos 3.1 workbench adf
    if os.path.isfile(amigaos_workbench_adf_path) and confirm("Use Amiga OS 3.1 Workbench adf (enter = yes)"):
        return amigaos_workbench_adf_path

    # amigaos workbench adf path
    amigaos_workbench_adf_path = os.path.join(path, "amiga-os-workbench.adf")
    if os.path.isfile(amigaos_workbench_adf_path):
        return amigaos_workbench_adf_path

    enter_amigaos_adf_path("Enter path to AmigaOS Workbench adf file: ", amigaos_workbench_adf_path)
    return amigaos_workbench_adf_path

def get_amigaos_install_adf_path(path, use_amigaos_31):
    # amigaos 3.1 install adf path
    amigaos_install_adf_path = os.path.join(path, "amiga-os-310-install.adf")

    # return amigaos 3.1 install adf path, it exists and use amigaos 3.1
    if os.path.isfile(amigaos_install_adf_path) and use_amigaos_31:
        return amigaos_install_adf_path

    # amigaos install adf path
    amigaos_install_adf_path = os.path.join(path, "amiga-os-install.adf")
    if os.path.isfile(amigaos_install_adf_path):
        return amigaos_install_adf_path

    enter_amigaos_adf_path("Enter path to AmigaOS Install adf file: ", amigaos_install_adf_path)
    return amigaos_install_adf_path

def install_minimal_amigaos(hst_imager_path, image_path):
    image_dir = os.path.dirname(image_path)
    if image_dir == None or image_dir == '':
        image_dir = '.'

    amigaos_workbench_adf_path = get_amigaos_workbench_adf_path(image_dir)
    use_amigaos_31 = os.path.basename(amigaos_workbench_adf_path) == "amiga-os-310-workbench.adf"
    amigaos_install_adf_path = get_amigaos_install_adf_path(image_dir, use_amigaos_31)

    # extract amiga os install adf to image file
    run_command([hst_imager_path, 'fs', 'extract', amigaos_install_adf_path, os.path.join(image_path, 'rdb', 'dh0')])

    # extract amiga os workbench adf to image file
    run_command([hst_imager_path, 'fs', 'extract', amigaos_workbench_adf_path, os.path.join(image_path, 'rdb', 'dh0')])

def install_kickstart13_rom(hst_imager_path, image_path):
    image_dir = os.path.dirname(image_path)
    if image_dir == None or image_dir == '':
        image_dir = '.'

    # a500 kickstart 1.3 rom path
    kickstart13_a500_rom_path = os.path.join(image_dir, "kick34005.A500")

    # enter a500 kickstart 1.3 rom path, if not present
    if not os.path.exists(kickstart13_a500_rom_path):
        enter_kickstart_rom_path("Enter path to Amiga 500 Kickstart 1.3 rom file", kickstart13_a500_rom_path)

    # copy kickstart 1.3 to image file
    run_command([hst_imager_path, 'fs', 'copy', kickstart13_a500_rom_path, os.path.join(image_path, 'rdb', 'dh0', 'Devs', 'Kickstarts')])

    # rom key path
    rom_key_path = os.path.join(image_path, 'rom.key')

    # copy rom key to image, if present
    if os.path.exists(rom_key_path):
        run_command([hst_imager_path, 'fs', 'copy', rom_key_path, os.path.join(image_path, 'rdb', 'dh0', 'Devs', 'Kickstarts')])

# install minimal whdload
def install_minimal_whdload(hst_imager_path, image_path):
    image_dir = os.path.dirname(image_path)
    if image_dir == None or image_dir == '':
        image_dir = '.'
    skick_lha_path = get_skick_lha_path(image_dir)
    whdload_usr_lha_path = get_whdload_lha_path(image_dir)
    iconlib_lha_path = get_iconlib_lha_path(image_dir)

    # extract soft-kicker lha to image file
    run_command([hst_imager_path, 'fs', 'extract', os.path.join(skick_lha_path, 'Kickstarts'), os.path.join(image_path, 'rdb', 'dh0', 'Devs', 'Kickstarts')])

    # extract whdload lha to image file
    run_command([hst_imager_path, 'fs', 'extract', os.path.join(whdload_usr_lha_path, os.path.join('WHDLoad', 'C')), os.path.join(image_path, 'rdb', 'dh0', 'C')])
    run_command([hst_imager_path, 'fs', 'extract', os.path.join(whdload_usr_lha_path, os.path.join('WHDLoad', 'S')), os.path.join(image_path, 'rdb', 'dh0', 'S')])

    # extract iconlib lha to image file
    run_command([hst_imager_path, 'fs', 'extract', os.path.join(iconlib_lha_path, 'IconLib_46.4', 'Libs', '68000', 'icon.library'), os.path.join(image_path, 'rdb', 'dh0', 'Libs')])
    run_command([hst_imager_path, 'fs', 'extract', os.path.join(iconlib_lha_path, 'IconLib_46.4', 'ThirdParty', 'RemLib', 'RemLib'), os.path.join(image_path, 'rdb', 'dh0', 'C')])
    run_command([hst_imager_path, 'fs', 'extract', os.path.join(iconlib_lha_path, 'IconLib_46.4', 'ThirdParty', 'LoadResident', 'LoadResident'), os.path.join(image_path, 'rdb', 'dh0', 'C')])

    # extract image file startup sequence
    run_command([hst_imager_path, 'fs', 'copy', os.path.join(image_path, 'rdb', 'dh0', 'S', 'Startup-Sequence'), image_dir])

    # read startup sequence
    startup_sequence_path = os.path.join(image_dir, 'Startup-Sequence')
    startup_sequence_lines = read_text_lines_for_amiga(startup_sequence_path)

    # create remlib lines for icon library
    remlib_lines = [
        "If EXISTS Libs:icon.library",
        "  RemLib >NIL: icon.library",
        "  If EXISTS Libs:workbench.library",
        "    RemLib >NIL: workbench.library",
        "  EndIf",
        "EndIf",
        ""
    ]

    # add remlib lines at beginning of startup sequence
    startup_sequence_lines = remlib_lines + startup_sequence_lines

    # write startup sequence
    write_text_lines_for_amiga(startup_sequence_path, startup_sequence_lines)

    # copy startup sequence to image file
    run_command([hst_imager_path, 'fs', 'copy', startup_sequence_path, os.path.join(image_path, 'rdb', 'dh0', 'S')])
