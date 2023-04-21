# Install Amiga OS 3.1
# --------------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2023-04-21
#
# A python script to install Amiga OS 3.1 adf files to an amiga harddisk file using Hst Imager console and Hst Amiga console.

"""Install Amiga OS 3.1"""

import os
import stat
import re
import shutil
import subprocess

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

# paths
current_path = os.getcwd()
script_path = os.path.dirname(__file__)
hst_amiga_path = os.path.join(current_path, 'hst.amiga')
hst_imager_path = os.path.join(current_path, 'hst.imager')

# use hst amiga exe app, if present
hst_amiga_dev = os.path.join(current_path, 'hst.amiga.exe')
if os.path.isfile(hst_amiga_dev):
    hst_amiga_path = hst_amiga_dev

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

# error, if hst amiga is not found
if not os.path.isfile(hst_amiga_path):
    print('Error: Hst Amiga file \'{0}\' not found. Download from https://github.com/henrikstengaard/hst-amiga/releases and extract next to Hst Imager.'.format(hst_amiga_path))
    exit(1)


workbench_adf_path = os.path.join(script_path, "amiga-os-310-workbench.adf")
locale_adf_path = os.path.join(script_path, "amiga-os-310-locale.adf")
extras_adf_path = os.path.join(script_path, "amiga-os-310-extras.adf")
fonts_adf_path = os.path.join(script_path, "amiga-os-310-fonts.adf")
install_adf_path = os.path.join(script_path, "amiga-os-310-install.adf")
storage_adf_path = os.path.join(script_path, "amiga-os-310-storage.adf")

src_adf_path = script_path

for adf_name in ["Workbench", "Locale", "Extras", "Fonts", "Install", "Storage"]:
    dest_adf_exists = False

    while not dest_adf_exists:
        dest_adf_filename = "amiga-os-310-{0}.adf".format(adf_name.lower())
        dest_adf_path = os.path.join(script_path, dest_adf_filename)
        dest_adf_exists = os.path.isfile(dest_adf_path)

        if dest_adf_exists:
            break
            
        adf_path = os.path.join(src_adf_path, dest_adf_filename)
        adf_exists = os.path.isfile(adf_path)

        if adf_exists:
            # copy detected adf path
            shutil.copyfile(adf_path, dest_adf_path)
            os.chmod(dest_adf_path, stat.S_IWRITE)

            break

        adf_path = input("Enter path to Amiga OS 3.1 {0} adf: ".format(adf_name))
        adf_exists = os.path.isfile(adf_path)

        if not adf_exists:
            print('Error: Amiga OS 3.1 {0} adf file \'{1}\' not found'.format(adf_name, adf_path))
            continue

        src_adf_path = os.path.dirname(adf_path)

        # copy entered adf path
        shutil.copyfile(adf_path, dest_adf_path)
        os.chmod(dest_adf_path, stat.S_IWRITE)

        break

# set image path
image_path = os.path.join(current_path, "amiga-os-310.vhd")
print('Creating image file \'{0}\''.format(image_path))

# show use pfs3 confirm dialog 
use_pfs3 = re.search(r'^(|y|yes)$', input("Use PFS3 file system? (enter = yes):"), re.I)

# create blank image of 500mb in size
run_command([hst_imager_path, 'blank', image_path, "500mb"])

# initialize rigid disk block for entire disk
run_command([hst_imager_path, 'rdb', 'init', image_path])

if use_pfs3:
    # add rdb file system pfs3aio with dos type PDS3
    run_command([hst_imager_path, 'rdb', 'fs', 'add', image_path, 'pfs3aio', 'PDS3'])

    # add rdb partition of entire disk with device name "DH0" and set bootable
    run_command([hst_imager_path, 'rdb', 'part', 'add', image_path, 'DH0', 'PDS3', '*', '--bootable'])
else:
    # add rdb file system fast file system with dos type DOS3 imported from amiga os install adf
    run_command([hst_imager_path, 'rdb', 'fs', 'import', image_path, install_adf_path, '--dos-type', 'DOS3', '--name', 'FastFileSystem'])

    # add rdb partition of entire disk with device name "DH0" and set bootable
    run_command([hst_imager_path, 'rdb', 'part', 'add', image_path, 'DH0', 'DOS3', '*', '--bootable'])

# format rdb partition number 1 with volume name "Workbench"
run_command([hst_imager_path, 'rdb', 'part', 'format', image_path, '1', 'Workbench'])


# extract workbench adf to image file
run_command([hst_imager_path, 'fs', 'extract', workbench_adf_path, os.path.join(image_path, 'rdb', 'dh0')])

# extract locale adf to image file
run_command([hst_imager_path, 'fs', 'extract', locale_adf_path, os.path.join(image_path, 'rdb', 'dh0', 'Locale')])

# extract extras adf to image file
run_command([hst_imager_path, 'fs', 'extract', extras_adf_path, os.path.join(image_path, 'rdb', 'dh0')])

# extract fonts adf to image file
run_command([hst_imager_path, 'fs', 'extract', fonts_adf_path, os.path.join(image_path, 'rdb', 'dh0', 'Fonts')])

# extract install adf to image file
run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'BRU'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'HDBackup'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'HDBackup.help'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'HDToolBox'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'HDBackup.info'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'HDToolBox.info'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'S', 'BRUtab'), os.path.join(image_path, 'rdb', 'dh0', 'S')])
run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'S', 'HDBackup.config'), os.path.join(image_path, 'rdb', 'dh0', 'S')])
run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'L', 'FastFileSystem'), os.path.join(image_path, 'rdb', 'dh0', 'L')])
run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'Libs', '*.library'), os.path.join(image_path, 'rdb', 'dh0', 'Libs')])
run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'Update', 'Disk.info'), os.path.join(image_path, 'rdb', 'dh0')])

# extract storage adf to image file
run_command([hst_imager_path, 'fs', 'extract', storage_adf_path, os.path.join(image_path, 'rdb', 'dh0', 'Storage')])

# copy icons from image file to local directory
run_command([hst_imager_path, 'fs', 'copy', os.path.join(image_path, 'rdb', 'dh0', '*.info'), os.path.join(current_path, 'icons'), '--recursive'])
shutil.copyfile(os.path.join(current_path, 'icons', 'Storage', 'Printers.info'), os.path.join(current_path, 'icons', 'Storage.info'))

# update icons
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Prefs.info')] + '-x 12 -y 20'.split(' '))
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Prefs', 'Printer.info')] + '-x 160 -y 48'.split(' '))

run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Utilities.info')] + '-x 98 -y 4'.split(' '))
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Utilities', 'Clock.info')] + '-x 91 -y 11'.split(' '))
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Utilities', 'MultiView.info')] + '-x 7 -y 4'.split(' '))

run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Tools.info')] + '-x 98 -y 38'.split(' '))
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Tools', 'IconEdit.info')] + '-x 111 -y 4'.split(' '))
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Tools', 'Commodities', 'Blanker.info')] + '-x 8 -y 84'.split(' '))
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Tools', 'Commodities', 'ClickToFront.info')] + '-x 99 -y 4'.split(' '))
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Tools', 'Commodities', 'CrossDOS.info')] + '-x 99 -y 44'.split(' '))
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Tools', 'Commodities', 'Exchange.info')] + '-x 8 -y 4'.split(' '))
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Tools', 'Commodities', 'FKey.info')] + '-x 99 -y 84'.split(' '))

run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'System.info')] + '-x 184 -y 4'.split(' '))
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'WBStartup.info')] + '-x 184 -y 38'.split(' '))
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Devs.info')] + '-x 270 -y 4'.split(' '))
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Storage.info')] + '-x 270 -y 38 -dx 480 -dy 77 -dw 107 -dh 199'.split(' '))
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Storage', 'Monitors.info')] + '-x 10 -y 106 -dx 480 -dy 77 -dw 107 -dh 199'.split(' '))
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Storage', 'Printers.info')] + '-x 10 -y 140 -dx 480 -dy 77 -dw 107 -dh 199'.split(' '))
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Expansion.info')] + '-x 356 -y 20'.split(' '))
run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Disk.info')] + '-dx 28 -dy 29 -dw 452 -dh 93'.split(' '))

# copy icons from local directory to image file
run_command([hst_imager_path, 'fs', 'copy', os.path.join(current_path, 'icons'), os.path.join(image_path, 'rdb', 'dh0'), '--recursive'])

print('Done')
