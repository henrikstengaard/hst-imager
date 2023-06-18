# Extract WHDLoads
# ----------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2023-06-04
#
# A python script to extract whdloads .lha files recursively from a directory
# to an amiga harddisk file and install minimal Amiga OS 3.1 from adf files using
# Hst Imager console.
#
# Requirements:
# - WHDload .lha and .zip files.
# - AmigaOS 3.1.4+ install adf for DOS7, if creating new image with DOS7 dostype.

"""Extract WHDLoads"""

import os
import re
import subprocess
import codecs
import unicodedata
import shared

# paths
current_path = os.getcwd()
script_path = os.path.dirname(__file__)
hst_imager_path = shared.get_hst_imager_path(script_path)

# enter whdloads directory to extract
whdloads_path = os.path.abspath(input('Enter WHDLoads directory to extract: '))

# error, if whdloads path is not found
if not os.path.isdir(whdloads_path):
    print('Error: WHDLoads directory \'{0}\' doesn\'t exist'.format(whdloads_path))
    exit(1)

# confirm create image confirm 
create_image = shared.confirm("Do you want to create a new hard disk image file?", "enter = yes")

image_path = None
if (create_image):
    # set image path
    image_path = os.path.join(current_path, 'whdloads.vhd')

    # create 16gb image file
    shared.create_image(hst_imager_path, image_path, '16gb')
else:
    # select image path
    image_path = shared.select_file_path('hard disk image file')
    
    # error, if image path is not found
    if not os.path.isfile(image_path):
        print('Error: Image path \'{0}\' doesn\'t exist'.format(image_path))
        exit(1)

# confirm install minimal whdload 
if (shared.confirm("Do you want to install minimal WHDLoad?", "enter = yes")):
    # install kickstart 1.3 rom
    shared.install_kickstart13_rom(hst_imager_path, image_path)

    # install minimal whdload input 
    shared.install_minimal_whdload(hst_imager_path, image_path)

# enter target directory whdloads are extracted to
target_dir = input('Target directory WHDLoads are extracted to (enter = DH1/WHDLoads): ')

# set default target directory, if not set or empty
if target_dir is None or target_dir == '':
    target_dir = 'DH1/WHDLoads'

# find .lha and .zip files and extract each to image
for root, directories, filenames in os.walk(whdloads_path):
    for filename in filenames:
        # skip, if filename doesn't end with .lha or .zip
        if not (filename.endswith(".lha") or filename.endswith(".zip")):
            continue

        index_dir = filename[0].upper()
        if re.search(r'^[0-9]', index_dir):
            index_dir = '0'

        whdload_file = os.path.join(root, filename)

        print(filename)

        # extract whdload lha to image file
        
        shared.run_command([hst_imager_path, 'fs', 'extract', whdload_file, os.path.join(*[image_path, 'rdb'] + target_dir.split('/') + [index_dir]), '--quiet'])

print('Done')
