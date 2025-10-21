#!/usr/bin/env python3
# Install AmigaOS 3.1
# -------------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2025-10-21
#
# A python script to install AmigaOS 3.1 adf files to an amiga harddisk
# image file using Hst Imager console and Hst Amiga console.
#
# Requirements:
# - Hst Amiga
# - AmigaOS 3.1 adf files
# - AmigaOS 3.1.4+ install adf for DOS7, if creating new image with DOS7 dostype.

"""Install AmigaOS 3.1"""

import os
import platform
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


# amigaos 3.1 files
amigaos_31_files = [
    {
        'Filename': 'amiga-os-310-install.adf',
        'Name': 'AmigaOS 3.1 Install Disk'
    },
    {
        'Filename': 'amiga-os-310-workbench.adf',
        'Name': 'AmigaOS 3.1 Workbench Disk'
    },
    {
        'Filename': 'amiga-os-310-extras.adf',
        'Name': 'AmigaOS 3.1 Extras Disk'
    },
    {
        'Filename': 'amiga-os-310-locale.adf',
        'Name': 'AmigaOS 3.1 Locale Disk'
    },
    {
        'Filename': 'amiga-os-310-fonts.adf',
        'Name': 'AmigaOS 3.1 Fonts Disk'
    },
    {
        'Filename': 'amiga-os-310-storage.adf',
        'Name': 'AmigaOS 3.1 Storage Disk'
    }
]

# get amigaos 3.1 files copied to current path
shared.get_adf_files(amigaos_31_files, current_path)

# confirm create image confirm 
create_image = shared.confirm("Do you want to create a new hard disk image file?", "enter = yes")

image_path = None
if (create_image):
    # set image path
    image_path = os.path.join(current_path, "amigaos-3.1.vhd")
    
    # create 16gb image file
    shared.create_image(hst_imager_path, image_path, '16gb')
else:
    # select image path
    image_path = shared.select_file_path('hard disk image file')
    
    # error, if image path is not found
    if not os.path.isfile(image_path):
        print('Error: Image path \'{0}\' doesn\'t exist'.format(image_path))
        exit(1)

# amigaos 3.1 adf paths
workbench_adf_path = os.path.join(current_path, "amiga-os-310-workbench.adf")
locale_adf_path = os.path.join(current_path, "amiga-os-310-locale.adf")
extras_adf_path = os.path.join(current_path, "amiga-os-310-extras.adf")
fonts_adf_path = os.path.join(current_path, "amiga-os-310-fonts.adf")
install_adf_path = os.path.join(current_path, "amiga-os-310-install.adf")
storage_adf_path = os.path.join(current_path, "amiga-os-310-storage.adf")

# extract workbench adf to image file
shared.run_command([hst_imager_path, 'fs', 'extract', workbench_adf_path, os.path.join(image_path, 'rdb', 'dh0')])

# extract locale adf to image file
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Locale')])
shared.run_command([hst_imager_path, 'fs', 'extract', locale_adf_path, os.path.join(image_path, 'rdb', 'dh0', 'Locale')])

# extract extras adf to image file
shared.run_command([hst_imager_path, 'fs', 'extract', extras_adf_path, os.path.join(image_path, 'rdb', 'dh0')])

# extract fonts adf to image file
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Fonts')])
shared.run_command([hst_imager_path, 'fs', 'extract', fonts_adf_path, os.path.join(image_path, 'rdb', 'dh0', 'Fonts')])

# extract install adf to image file
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'BRU'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'HDBackup'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'HDBackup.help'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'HDToolBox'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'HDBackup.info'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'HDToolBox.info'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'S')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'S', 'BRUtab'), os.path.join(image_path, 'rdb', 'dh0', 'S')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'S', 'HDBackup.config'), os.path.join(image_path, 'rdb', 'dh0', 'S')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'L')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'L', 'FastFileSystem'), os.path.join(image_path, 'rdb', 'dh0', 'L')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Libs')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'Libs', '*.library'), os.path.join(image_path, 'rdb', 'dh0', 'Libs')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'Update', 'Disk.info'), os.path.join(image_path, 'rdb', 'dh0')])

# extract storage adf to image file
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Storage')])
shared.run_command([hst_imager_path, 'fs', 'extract', storage_adf_path, os.path.join(image_path, 'rdb', 'dh0', 'Storage')])

# create temp directory
temp_path = os.path.join(current_path, 'temp')
if os.path.exists(temp_path):
    shutil.rmtree(temp_path)

os.mkdir(temp_path)

# copy icons from image file to local directory
icons_path = os.path.join(temp_path, 'icons')
shared.run_command([hst_imager_path, 'fs', 'copy', os.path.join(image_path, 'rdb', 'dh0', '*.info'), icons_path, '--recursive', '--makedir'])
shutil.copyfile(os.path.join(icons_path, 'Storage', 'Printers.info'), os.path.join(icons_path, 'Storage.info'))

# update icons
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Prefs.info')] + '-x 12 -y 20'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Prefs', 'Printer.info')] + '-x 160 -y 48'.split(' '))

shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Utilities.info')] + '-x 98 -y 4'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Utilities', 'Clock.info')] + '-x 91 -y 11'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Utilities', 'MultiView.info')] + '-x 7 -y 4'.split(' '))

shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Tools.info')] + '-x 98 -y 38'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Tools', 'IconEdit.info')] + '-x 111 -y 4'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Tools', 'Commodities', 'Blanker.info')] + '-x 8 -y 84'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Tools', 'Commodities', 'ClickToFront.info')] + '-x 99 -y 4'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Tools', 'Commodities', 'CrossDOS.info')] + '-x 99 -y 44'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Tools', 'Commodities', 'Exchange.info')] + '-x 8 -y 4'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Tools', 'Commodities', 'FKey.info')] + '-x 99 -y 84'.split(' '))

shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'System.info')] + '-x 184 -y 4'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'WBStartup.info')] + '-x 184 -y 38'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Devs.info')] + '-x 270 -y 4'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Storage.info')] + '-x 270 -y 38 -dx 480 -dy 77 -dw 107 -dh 199'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Storage', 'Monitors.info')] + '-x 10 -y 106 -dx 480 -dy 77 -dw 107 -dh 199'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Storage', 'Printers.info')] + '-x 10 -y 140 -dx 480 -dy 77 -dw 107 -dh 199'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Expansion.info')] + '-x 356 -y 20'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Disk.info')] + '-dx 28 -dy 29 -dw 452 -dh 93'.split(' '))

# copy icons from local directory to image file
shared.run_command([hst_imager_path, 'fs', 'copy', icons_path, os.path.join(image_path, 'rdb', 'dh0'), '--recursive'])

print('Done')
