# Install minimal WHDLoad
# -----------------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2023-04-21
#
# A python script to install minimal WHDLoad requires files
# to an amiga harddisk file using Hst Imager console.

"""Install minimal WHDLoad"""

import os
import re
import stat
import shutil
import subprocess
import sys
import urllib.request

# run command
def run_command(commands):
    """Run command"""

    # process to run commands
    process = subprocess.run(commands)

    # return, if return code is not 0
    if process.returncode:
        print(stderr)
        exit(1)

def enter_amiga_os_adf_file(title, path):
    adf_file = input("{0}: ".format(title))

    if not os.path.exists(adf_file):
        print('Error: Adf file \'{0}\' doesn\'t exist'.format(adf_file))
        exit(1)

    # copy adf file
    shutil.copyfile(
        adf_file,
        path)
    os.chmod(path, stat.S_IWRITE)

def enter_kickstart_rom_file(title, path):
    rom_file = input("{0}: ".format(title))

    if not os.path.exists(rom_file):
        print('Error: Rom file \'{0}\' doesn\'t exist'.format(rom_file))
        exit(1)

    # copy rom file
    shutil.copyfile(
        rom_file,
        path)
    os.chmod(path, stat.S_IWRITE)
    
    # copy rom key, if present
    rom_key_file = os.path.join(os.path.dirname(rom_file), "rom.key")
    if os.path.exists(rom_key_file):
        dest_rom_key_file = os.path.join(os.path.dirname(path, "rom.key"))
        shutil.copyfile(
            rom_key_file,
            dest_rom_key_file)
        os.chmod(dest_rom_key_file, stat.S_IWRITE)

# paths
current_path = os.getcwd()
script_path = os.path.dirname(__file__)
hst_imager_path = os.path.join(current_path, 'hst.imager')

# get patch only argument
image_path = None
if len(sys.argv) >= 2 and re.search(r'--image-path', sys.argv[1]):
    image_path = sys.argv[2]

# use hst imager exe app, if present
hst_imager_dev = os.path.join(current_path, 'hst.imager.exe')
if os.path.isfile(hst_imager_dev):
    hst_imager_path = hst_imager_dev

# use hst imager development app, if present
hst_imager_dev = os.path.join(current_path, 'Hst.Imager.ConsoleApp.exe')
if os.path.isfile(hst_imager_dev):
    hst_imager_path = hst_imager_dev

# error, if hst imager is not found
if not os.path.isfile(hst_imager_path):
    print('Error: Hst Imager file \'{0}\' not found'.format(hst_imager_path))
    exit(1)

# error, if image path is not found
if not os.path.isfile(image_path):
    print('Error: Image path \'{0}\' not found'.format(image_path))
    exit(1)

# download skick lha from animet, if not found
skick_lha_path = os.path.join(script_path, "skick346.lha")
if not os.path.isfile(skick_lha_path):
    url = 'https://aminet.net/util/boot/skick346.lha'
    print('Downloading url \'{0}\''.format(url))
    urllib.request.urlretrieve(url, skick_lha_path)

# download whdload usr lha from animet, if not found
whdload_usr_lha_path = os.path.join(script_path, "WHDLoad_usr.lha")
if not os.path.isfile(whdload_usr_lha_path):
    url = 'https://whdload.de/whdload/WHDLoad_usr.lha'
    print('Downloading url \'{0}\''.format(url))
    urllib.request.urlretrieve(url, whdload_usr_lha_path)

# select and copy amiga os workbench adf, if not present
amiga_os_workbench_adf_path = os.path.join(script_path, "amiga-os-workbench.adf")
if not os.path.exists(amiga_os_workbench_adf_path):
    enter_amiga_os_adf_file("Enter path to Amiga OS Workbench adf", amiga_os_workbench_adf_path)

# select and copy amiga os install adf, if not present
amiga_os_install_adf_path = os.path.join(script_path, "amiga-os-install.adf")
if not os.path.exists(amiga_os_install_adf_path):
    enter_amiga_os_adf_file("Enter path to Amiga OS Install adf", amiga_os_install_adf_path)

# select and copy amiga os install adf, if not present
kickstart13_a500_rom_path = os.path.join(script_path, "kick34005.A500")
if not os.path.exists(kickstart13_a500_rom_path):
    enter_kickstart_rom_file("Enter path to Amiga 500 Kickstart 1.3 rom file", kickstart13_a500_rom_path)

# extract amiga os install adf to image file
run_command([hst_imager_path, 'fs', 'extract', amiga_os_install_adf_path, os.path.join(image_path, 'rdb', 'dh0')])

# extract amiga os workbench adf to image file
run_command([hst_imager_path, 'fs', 'extract', amiga_os_workbench_adf_path, os.path.join(image_path, 'rdb', 'dh0')])

# copy kickstart 1.3 to image file
run_command([hst_imager_path, 'fs', 'copy', kickstart13_a500_rom_path, os.path.join(image_path, 'rdb', 'dh0', 'Devs', 'Kickstarts')])

# copy rom.key to image file, if present
rom_key_path = os.path.join(script_path, 'rom.key')
if os.path.exists(rom_key_path):
    run_command([hst_imager_path, 'fs', 'copy', rom_key_path, os.path.join(image_path, 'rdb', 'dh0', 'Devs', 'Kickstarts')])

# extract soft-kicker lha to image file
run_command([hst_imager_path, 'fs', 'extract', os.path.join(skick_lha_path, 'Kickstarts'), os.path.join(image_path, 'rdb', 'dh0', 'Devs', 'Kickstarts')])

# extract whdload lha to image file
run_command([hst_imager_path, 'fs', 'extract', os.path.join(whdload_usr_lha_path, os.path.join('WHDLoad', 'C')), os.path.join(image_path, 'rdb', 'dh0', 'C')])
run_command([hst_imager_path, 'fs', 'extract', os.path.join(whdload_usr_lha_path, os.path.join('WHDLoad', 'S')), os.path.join(image_path, 'rdb', 'dh0', 'S')])

print('Done')
