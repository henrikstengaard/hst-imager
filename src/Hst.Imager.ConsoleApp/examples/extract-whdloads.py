# Extract WHDLoads
# ----------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2023-04-21
#
# A python script to extract whdloads .lha files recursively from a directory
# to an amiga harddisk file and install minimal Amiga OS 3.1 from adf files using
# Hst Imager console.

"""Extract WHDLoads"""

import os
import re
import subprocess
import codecs
import unicodedata

# run command
def run_command(commands):
    """Run command"""

    # process to run commands
    process = subprocess.run(commands)

    # return, if return code is not 0
    if process.returncode:
        print(stderr)
        exit(1)

# paths
current_path = os.getcwd()
script_path = os.path.dirname(__file__)
hst_imager_path = os.path.join(current_path, 'hst.imager')

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

# enter whdloads directory to extract
whdloads_path = input('Enter WHDLoads directory to extract: ')

# error, if hst imager is not found
if not os.path.isdir(whdloads_path):
    print('Error: WHDLoads directory \'{0}\' doesn\'t exist'.format(whdloads_path))
    exit(1)


# show create image confirm input 
create_image = re.search(r'^(|y|yes)$', input("Do you want to create a new image file? (enter = yes):"), re.I)

image_path = None
if (create_image):
    # set image path
    image_path = os.path.join(current_path, 'whdloads.vhd')
    print('Creating image file \'{0}\''.format(image_path))
    
    # create blank image of calculated disk size
    run_command([hst_imager_path, 'blank', image_path, '16gb'])
    
    # initialize rigid disk block for entire disk
    run_command([hst_imager_path, 'rdb', 'init', image_path])
    
    # add rdb file system pfs3aio with dos type PDS3
    run_command([hst_imager_path, 'rdb', 'fs', 'add', image_path, 'pfs3aio', 'PDS3'])
    
    # add rdb partition of 500mb disk space with device name "DH0" and set bootable
    run_command([hst_imager_path, 'rdb', 'part', 'add', image_path, 'DH0', 'PDS3', '500mb', '--bootable'])
    
    # add rdb partition of remaining disk space with device name "DH1"
    run_command([hst_imager_path, 'rdb', 'part', 'add', image_path, 'DH1', 'PDS3', '*'])
    
    # format rdb partition number 1 with volume name "Workbench"
    run_command([hst_imager_path, 'rdb', 'part', 'format', image_path, '1', 'Workbench'])
    
    # format rdb partition number 2 with volume name "Work"
    run_command([hst_imager_path, 'rdb', 'part', 'format', image_path, '2', 'Work'])
else:
    # enter whdloads directory to extract
    image_path = input('Enter image path to extract WHDLoads to: ')
    
    # error, if image path is not found
    if not os.path.isfile(image_path):
        print('Error: Image path \'{0}\' doesn\'t exist'.format(image_path))
        exit(1)

# show install minimal whdload input 
if (re.search(r'^(|y|yes)$', input("Do you want to install minimal WHDLoad? (enter = yes):"), re.I)):
    # run install minimal whdload script
    run_command(['python', os.path.join(script_path, 'install-minimal-whdload.py'), '--image-path --no-amiga-os', image_path])

# enter target directory whdloads are extracted to
target_dir = input('Target directory WHDLoads are extracted to (enter = DH1\WHDLoads): ')

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
        
        run_command([hst_imager_path, 'fs', 'extract', whdload_file, os.path.join(*[image_path, 'rdb'] + target_dir.split('/') + [index_dir]), '--quiet'])

print('Done')
