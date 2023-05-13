﻿# Install Amiga OS 3.1
# --------------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2023-05-14
#
# A python script to install AmigaOS 3.1 adf files to an amiga harddisk file
# using Hst Imager console and Hst Amiga console.
#
# Requirements:
# - Hst Amiga
# - AmigaOS 3.1 adf files
# - AmigaOS 3.1.4+ install adf for DOS7, if creating new image with DOS7 dostype.

"""Install Amiga OS 3.1"""

import os
import stat
import re
import shutil
import subprocess
import shared


# paths
current_path = os.getcwd()
script_path = os.path.dirname(__file__)
hst_imager_path = shared.get_hst_imager_path(script_path)
hst_amiga_path = shared.get_hst_amiga_path(script_path)

# amiga os 3.1 adf paths
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
            os.chmod(dest_adf_path, os.stat(dest_adf_path).st_mode | stat.S_IREAD | stat.S_IWRITE | stat.S_IEXEC)

            break

        adf_path = os.path.abspath(input("Enter path to Amiga OS 3.1 {0} adf file: ".format(adf_name)))
        adf_exists = os.path.isfile(adf_path)

        if not adf_exists:
            print('Error: Amiga OS 3.1 {0} adf file \'{1}\' not found'.format(adf_name, adf_path))
            continue

        src_adf_path = os.path.dirname(adf_path)

        # copy entered adf path
        shutil.copyfile(adf_path, dest_adf_path)
        os.chmod(dest_adf_path, os.stat(dest_adf_path).st_mode | stat.S_IREAD | stat.S_IWRITE | stat.S_IEXEC)

        break

# set image path
image_path = os.path.join(current_path, "amigaos-310.vhd")

# create 16gb image file
shared.create_image(hst_imager_path, image_path, '16gb')

# extract workbench adf to image file
shared.run_command([hst_imager_path, 'fs', 'extract', workbench_adf_path, os.path.join(image_path, 'rdb', 'dh0')])

# extract locale adf to image file
shared.run_command([hst_imager_path, 'fs', 'extract', locale_adf_path, os.path.join(image_path, 'rdb', 'dh0', 'Locale')])

# extract extras adf to image file
shared.run_command([hst_imager_path, 'fs', 'extract', extras_adf_path, os.path.join(image_path, 'rdb', 'dh0')])

# extract fonts adf to image file
shared.run_command([hst_imager_path, 'fs', 'extract', fonts_adf_path, os.path.join(image_path, 'rdb', 'dh0', 'Fonts')])

# extract install adf to image file
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'BRU'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'HDBackup'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'HDBackup.help'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'HDToolBox'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'HDBackup.info'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'HDToolBox.info'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'S', 'BRUtab'), os.path.join(image_path, 'rdb', 'dh0', 'S')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'S', 'HDBackup.config'), os.path.join(image_path, 'rdb', 'dh0', 'S')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'L', 'FastFileSystem'), os.path.join(image_path, 'rdb', 'dh0', 'L')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'Libs', '*.library'), os.path.join(image_path, 'rdb', 'dh0', 'Libs')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'Update', 'Disk.info'), os.path.join(image_path, 'rdb', 'dh0')])

# extract storage adf to image file
shared.run_command([hst_imager_path, 'fs', 'extract', storage_adf_path, os.path.join(image_path, 'rdb', 'dh0', 'Storage')])

# copy icons from image file to local directory
shared.run_command([hst_imager_path, 'fs', 'copy', os.path.join(image_path, 'rdb', 'dh0', '*.info'), os.path.join(current_path, 'icons'), '--recursive'])
shutil.copyfile(os.path.join(current_path, 'icons', 'Storage', 'Printers.info'), os.path.join(current_path, 'icons', 'Storage.info'))

# update icons
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Prefs.info')] + '-x 12 -y 20'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Prefs', 'Printer.info')] + '-x 160 -y 48'.split(' '))

shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Utilities.info')] + '-x 98 -y 4'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Utilities', 'Clock.info')] + '-x 91 -y 11'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Utilities', 'MultiView.info')] + '-x 7 -y 4'.split(' '))

shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Tools.info')] + '-x 98 -y 38'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Tools', 'IconEdit.info')] + '-x 111 -y 4'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Tools', 'Commodities', 'Blanker.info')] + '-x 8 -y 84'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Tools', 'Commodities', 'ClickToFront.info')] + '-x 99 -y 4'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Tools', 'Commodities', 'CrossDOS.info')] + '-x 99 -y 44'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Tools', 'Commodities', 'Exchange.info')] + '-x 8 -y 4'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Tools', 'Commodities', 'FKey.info')] + '-x 99 -y 84'.split(' '))

shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'System.info')] + '-x 184 -y 4'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'WBStartup.info')] + '-x 184 -y 38'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Devs.info')] + '-x 270 -y 4'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Storage.info')] + '-x 270 -y 38 -dx 480 -dy 77 -dw 107 -dh 199'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Storage', 'Monitors.info')] + '-x 10 -y 106 -dx 480 -dy 77 -dw 107 -dh 199'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Storage', 'Printers.info')] + '-x 10 -y 140 -dx 480 -dy 77 -dw 107 -dh 199'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Expansion.info')] + '-x 356 -y 20'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(current_path, 'icons', 'Disk.info')] + '-dx 28 -dy 29 -dw 452 -dh 93'.split(' '))

# copy icons from local directory to image file
shared.run_command([hst_imager_path, 'fs', 'copy', os.path.join(current_path, 'icons'), os.path.join(image_path, 'rdb', 'dh0'), '--recursive'])

print('Done')
