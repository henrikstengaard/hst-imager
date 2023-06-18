# Install AmigaOS 3.2
# -------------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2023-06-04
#
# A python script to install Amiga OS 3.1 adf files to an amiga harddisk file
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
    }
]

# get amigaos 3.2 files copied to current path
shared.get_amigaos_files(amigaos_32_files, current_path)

# amigaos 3.1 adf paths
install_adf_path = os.path.join(current_path, "Install3.2.adf")
workbench_adf_path = os.path.join(current_path, "Workbench3.2.adf")
extras_adf_path = os.path.join(current_path, "Extras3.2.adf")
classes_adf_path = os.path.join(current_path, "Classes3.2.adf")
fonts_adf_path = os.path.join(current_path, "Fonts.adf")
storage_adf_path = os.path.join(current_path, "Storage3.2.adf")

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

install_path = os.path.join(current_path, 'temp', 'install')
if os.path.exists(install_path):
    shutil.rmtree(install_path)
    
os.makedirs(os.path.join(install_path, 'Prefs'))
os.makedirs(os.path.join(install_path, 'Prefs', 'Env-Archive'))
os.makedirs(os.path.join(install_path, 'Prefs', 'Env-Archive', 'Sys'))
os.makedirs(os.path.join(install_path, 'Prefs', 'Env-Archive', 'Versions'))
os.makedirs(os.path.join(install_path, 'Prefs', 'Presets'))
os.makedirs(os.path.join(install_path, 'Prefs', 'Presets', 'Backdrops'))
os.makedirs(os.path.join(install_path, 'Prefs', 'Presets', 'Pointers'))
os.makedirs(os.path.join(install_path, 'Fonts'))
os.makedirs(os.path.join(install_path, 'Expansion'))
os.makedirs(os.path.join(install_path, 'WBStartup'))
os.makedirs(os.path.join(install_path, 'Locale'))
os.makedirs(os.path.join(install_path, 'Locale', 'Catalogs'))
os.makedirs(os.path.join(install_path, 'Locale', 'Languages'))
os.makedirs(os.path.join(install_path, 'Locale', 'Countries'))
os.makedirs(os.path.join(install_path, 'Locale', 'Help'))
os.makedirs(os.path.join(install_path, 'Classes'))
os.makedirs(os.path.join(install_path, 'Classes', 'Gadgets'))
os.makedirs(os.path.join(install_path, 'Classes', 'DataTypes'))
os.makedirs(os.path.join(install_path, 'Classes', 'Images'))
os.makedirs(os.path.join(install_path, 'Devs'))
os.makedirs(os.path.join(install_path, 'Devs', 'Monitors'))
os.makedirs(os.path.join(install_path, 'Devs', 'DataTypes'))
os.makedirs(os.path.join(install_path, 'Devs', 'DOSDrivers'))
os.makedirs(os.path.join(install_path, 'Devs', 'Printers'))
os.makedirs(os.path.join(install_path, 'Devs', 'Keymaps'))
os.makedirs(os.path.join(install_path, 'Storage'))
os.makedirs(os.path.join(install_path, 'Storage', 'DOSDrivers'))
os.makedirs(os.path.join(install_path, 'Storage', 'Printers'))
os.makedirs(os.path.join(install_path, 'Storage', 'Monitors'))
os.makedirs(os.path.join(install_path, 'Storage', 'Keymaps'))
os.makedirs(os.path.join(install_path, 'Storage', 'DataTypes'))

os.makedirs(os.path.join(install_path, 'Libs'))
os.makedirs(os.path.join(install_path, 'Tools'))
os.makedirs(os.path.join(install_path, 'System'))

os.makedirs(os.path.join(install_path, 'L'))
os.makedirs(os.path.join(install_path, 'S'))

shared.run_command([hst_imager_path, 'fs', 'copy', install_path, os.path.join(image_path, 'rdb', 'dh0'), '--recursive'])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'C'), os.path.join(image_path, 'rdb', 'dh0', 'C')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'HDTools', 'hd*'), os.path.join(image_path, 'rdb', 'dh0', 'Tools')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'Installer'), os.path.join(image_path, 'rdb', 'dh0', 'System')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'Libs', 'workbench.library'), os.path.join(image_path, 'rdb', 'dh0', 'Libs')])

shared.run_command([hst_imager_path, 'fs', 'extract', os.path.join(install_adf_path, 'Libs', 'icon.library'), os.path.join(image_path, 'rdb', 'dh0', 'Libs')])


update_path = os.path.join(current_path, 'temp', 'update')
if os.path.exists(update_path):
    shutil.rmtree(update_path)

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

print('Done')
