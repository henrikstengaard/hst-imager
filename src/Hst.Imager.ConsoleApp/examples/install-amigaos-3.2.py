#!/usr/bin/env python3
# Install AmigaOS 3.2
# -------------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2025-10-11
#
# A python script to install Amiga OS 3.2 adf files to an amiga harddisk file
# using Hst Imager console and Hst Amiga console.
#
# Requirements:
# - Hst Amiga
# - AmigaOS 3.2 adf files

"""Install AmigaOS 3.2"""

import os
import shutil
import shared


# paths
current_path = os.getcwd()
script_path = os.path.dirname(__file__)
hst_imager_path = shared.get_hst_imager_path(script_path)
hst_amiga_path = shared.get_hst_amiga_path(script_path)

# amigaos 3.2 files
amigaos_32_files = [
    {
        'Filename': 'Install3.2.adf',
        'Name': 'AmigaOS 3.2 Install Disk'
    },
    {
        'Filename': 'Workbench3.2.adf',
        'Name': 'AmigaOS 3.2 Workbench Disk'
    },
    {
        'Filename': 'Extras3.2.adf',
        'Name': 'AmigaOS 3.2 Extras Disk'
    },
    {
        'Filename': 'Classes3.2.adf',
        'Name': 'AmigaOS 3.2 Classes Disk'
    },
    {
        'Filename': 'Fonts.adf',
        'Name': 'AmigaOS 3.2 Fonts Disk'
    },
    {
        'Filename': 'Storage3.2.adf',
        'Name': 'AmigaOS 3.2 Storage Disk'
    },
    {
        'Filename': 'DiskDoctor.adf',
        'Name': 'AmigaOS 3.2 Disk Doctor'
    },
    {
        'Filename': 'MMULibs.adf',
        'Name': 'AmigaOS 3.2 MMULibs'
    }
]

# get amigaos 3.2 files copied to current path
shared.get_adf_files(amigaos_32_files, current_path)

# amigaos 3.2 adf paths
install_adf_path = os.path.join(current_path, "Install3.2.adf")
workbench_adf_path = os.path.join(current_path, "Workbench3.2.adf")
extras_adf_path = os.path.join(current_path, "Extras3.2.adf")
classes_adf_path = os.path.join(current_path, "Classes3.2.adf")
fonts_adf_path = os.path.join(current_path, "Fonts.adf")
storage_adf_path = os.path.join(current_path, "Storage3.2.adf")
diskdoctor_adf_path = os.path.join(current_path, "DiskDoctor.adf")
mmulibs_adf_path = os.path.join(current_path, "MMULibs.adf")

# confirm create image confirm 
create_image = shared.confirm("Do you want to create a new hard disk image file?", "enter = yes")

image_path = None
if (create_image):
    # set image path
    image_path = os.path.join(current_path, "amigaos-3.2.vhd")
    
    # create 16gb image file
    shared.create_image(hst_imager_path, image_path, '16gb')
else:
    # select image path
    image_path = shared.select_file_path('hard disk image file')
    
    # error, if image path is not found
    if not os.path.isfile(image_path):
        print('Error: Image path \'{0}\' doesn\'t exist'.format(image_path))
        exit(1)

shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Prefs')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Prefs', 'Env-Archive')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Prefs', 'Env-Archive', 'Sys')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Prefs', 'Env-Archive', 'Versions')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Prefs', 'Presets')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Prefs', 'Presets', 'Backdrops')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Prefs', 'Presets', 'Pointers')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Fonts')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Expansion')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'WBStartup')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Locale')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Locale', 'Catalogs')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Locale', 'Languages')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Locale', 'Countries')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Locale', 'Help')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Classes')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Classes', 'Gadgets')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Classes', 'DataTypes')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Classes', 'Images')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Devs')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Devs', 'Monitors')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Devs', 'DataTypes')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Devs', 'DOSDrivers')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Devs', 'Printers')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Devs', 'Keymaps')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Storage')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Storage', 'DOSDrivers')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Storage', 'Printers')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Storage', 'Monitors')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Storage', 'Keymaps')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Storage', 'DataTypes')])

shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Libs')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'Tools')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'System')])

shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'C')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'L')])
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'S')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'C'), os.path.join(image_path, 'rdb', 'dh0', 'C')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'hd*'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'Installer'), os.path.join(image_path, 'rdb', 'dh0', 'System')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'Libs', 'workbench.library'), os.path.join(image_path, 'rdb', 'dh0', 'Libs')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'Libs', 'icon.library'), os.path.join(image_path, 'rdb', 'dh0', 'Libs')])


update_path = os.path.join(current_path, 'temp', 'update')
if os.path.exists(update_path):
    shutil.rmtree(update_path)

os.mkdir(update_path)
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'Update'), update_path])

# copy fastfilesystem
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'L', 'FastFileSystem'), os.path.join(image_path, 'rdb', 'dh0', 'L')])


# workbench
# ---------

shared.run_command([hst_imager_path, 'fs', 'extract', workbench_adf_path, os.path.join(image_path, 'rdb', 'dh0')])

# extras
# ------

#Copy >NIL: "$amigaosdisk:~(Disk.info|S)" "SYSTEMDIR:" ALL CLONE
#Copy >NIL: "$amigaosdisk:S/~(user-startup)" "SYSTEMDIR:S" ALL CLONE
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(extras_adf_path, '*.info'), os.path.join(image_path, 'rdb', 'dh0'), '--recursive', 'false'])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(extras_adf_path, 'L'), os.path.join(image_path, 'rdb', 'dh0', 'L')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(extras_adf_path, 'Prefs'), os.path.join(image_path, 'rdb', 'dh0', 'Prefs')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(extras_adf_path, 'System'), os.path.join(image_path, 'rdb', 'dh0', 'System')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(extras_adf_path, 'Tools'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])

s_path = os.path.join(current_path, 'temp', 's')
if os.path.exists(s_path):
    shutil.rmtree(s_path)

os.mkdir(s_path)
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(extras_adf_path, 'S'), s_path])
os.remove(os.path.join(s_path, 'User-startup'))

shared.run_command([hst_imager_path, 'fs', 'copy', s_path, os.path.join(image_path, 'rdb', 'dh0', 'S')])

# classes
# -------

shared.run_command([hst_imager_path, 'fs', 'extract', classes_adf_path, os.path.join(image_path, 'rdb', 'dh0')])

# fonts
# -----

shared.run_command([hst_imager_path, 'fs', 'extract', fonts_adf_path, os.path.join(image_path, 'rdb', 'dh0', 'Fonts')])

# storage
# -------

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'DataTypes.info'), os.path.join(image_path, 'rdb', 'dh0', 'Storage')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'DOSDrivers.info'), os.path.join(image_path, 'rdb', 'dh0', 'Storage')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'Keymaps.info'), os.path.join(image_path, 'rdb', 'dh0', 'Storage')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'Monitors.info'), os.path.join(image_path, 'rdb', 'dh0', 'Storage')])
shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'Printers.info'), os.path.join(image_path, 'rdb', 'dh0', 'Storage')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'Classes', 'DataTypes'), os.path.join(image_path, 'rdb', 'dh0', 'Classes', 'DataTypes')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'C'), os.path.join(image_path, 'rdb', 'dh0', 'C')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'DefIcons', '*.info'), os.path.join(image_path, 'rdb', 'dh0', 'Prefs', 'Env-Archive', 'Sys')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'Presets', 'Pointers'), os.path.join(image_path, 'rdb', 'dh0', 'Prefs', 'Presets', 'Pointers')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'Monitors'), os.path.join(image_path, 'rdb', 'dh0', 'Storage', 'Monitors')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'DOSDrivers'), os.path.join(image_path, 'rdb', 'dh0', 'Storage', 'DOSDrivers')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'WBStartup'), os.path.join(image_path, 'rdb', 'dh0', 'WBStartup')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'Env-Archive', 'deficons.prefs'), os.path.join(image_path, 'rdb', 'dh0', 'Prefs', 'Env-Archive')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'Env-Archive', 'Pointer.prefs'), os.path.join(image_path, 'rdb', 'dh0', 'Prefs', 'Env-Archive', 'Sys')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'Printers'), os.path.join(image_path, 'rdb', 'dh0', 'Devs', 'Printers')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'Keymaps'), os.path.join(image_path, 'rdb', 'dh0', 'Devs', 'Keymaps')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(storage_adf_path, 'LIBS'), os.path.join(image_path, 'rdb', 'dh0', 'Libs')])


# finalize
# --------

# copy disk.info
shared.run_command([hst_imager_path, 'fs', 'copy', os.path.join(update_path, 'disk.info'), os.path.join(image_path, 'rdb', 'dh0')])

# copy release to versions
shared.run_command([hst_imager_path, 'fs', 'copy', os.path.join(update_path, 'Release'), os.path.join(image_path, 'rdb', 'dh0', 'Prefs', 'Env-Archive', 'Versions'), '--recursive'])

# copy startup-sequence
startup_harddrive_path = os.path.join(update_path, 'Startup-HardDrive')
startup_sequence_path = os.path.join(update_path, 'Startup-sequence')
if os.path.isfile(startup_sequence_path):
    os.remove(startup_sequence_path)
os.rename(startup_harddrive_path, startup_sequence_path)
shared.run_command([hst_imager_path, 'fs', 'copy', startup_sequence_path, os.path.join(image_path, 'rdb', 'dh0', 'S')])


# clean up
# --------

# copy icons from image file to local directory
icons_path = os.path.join(current_path, 'temp', 'icons')
if os.path.exists(icons_path):
    shutil.rmtree(icons_path)

os.mkdir(icons_path)
shared.run_command([hst_imager_path, 'fs', 'copy', os.path.join(image_path, 'rdb', 'dh0', '*.info'), icons_path, '--recursive'])

# update icons
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Prefs.info')] + '-x 12 -y 20'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Prefs', 'Printer.info')] + '-x 160 -y 48'.split(' '))

shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Utilities.info')] + '-x 98 -y 4'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Utilities', 'Clock.info')] + '-x 91 -y 11'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Utilities', 'MultiView.info')] + '-x 11 -y 11'.split(' '))

shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Tools.info')] + '-x 98 -y 38'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Tools', 'IconEdit.info')] + '-x 111 -y 45'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Tools', 'HDToolBox.info')] + '-x 202 -y 4'.split(' '))

shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'System.info')] + '-x 184 -y 4 -dh 150'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'WBStartup.info')] + '-x 184 -y 38'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Devs.info')] + '-x 270 -y 4'.split(' '))

shutil.copyfile(os.path.join(icons_path, 'Devs.info'), os.path.join(icons_path, 'Storage.info'))

shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Storage.info')] + '-x 270 -y 38 -dx 480 -dy 77 -dw 110 -dh 199'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Storage', 'Monitors.info')] + '-dx 156 -dy 77 -dw 270 -dh 199'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Storage', 'Printers.info')] + '-dx 480 -dy 77 -dw 107 -dh 199'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Expansion.info')] + '-x 356 -y 20'.split(' '))
shared.run_command([hst_amiga_path, 'icon', 'update', os.path.join(icons_path, 'Disk.info')] + '-dx 28 -dy 29 -dw 462 -dh 103'.split(' '))

# copy icons from local directory to image file
shared.run_command([hst_imager_path, 'fs', 'copy', icons_path, os.path.join(image_path, 'rdb', 'dh0'), '--recursive'])

# copy files from disk doctor for mounting adf in amigaos
if os.path.exists(diskdoctor_adf_path):
    shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(diskdoctor_adf_path, 'C', 'DAControl'), os.path.join(image_path, 'rdb', 'dh0', 'C')])
    shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(diskdoctor_adf_path, 'Devs', 'trackfile.device'), os.path.join(image_path, 'rdb', 'dh0', 'Devs')])

# copy files from mmulibs
if os.path.exists(mmulibs_adf_path):
    shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(mmulibs_adf_path, 'C'), os.path.join(image_path, 'rdb', 'dh0', 'C')])
    shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(mmulibs_adf_path, 'Libs'), os.path.join(image_path, 'rdb', 'dh0', 'Libs'), '--recursive'])
    shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(mmulibs_adf_path, 'Locale'), os.path.join(image_path, 'rdb', 'dh0', 'Locale'), '--recursive'])

print('Done')
