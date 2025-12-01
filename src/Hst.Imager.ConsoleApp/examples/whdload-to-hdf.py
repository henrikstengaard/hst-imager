#!/usr/bin/env python3
# WHDLoad to HDF
# --------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2025-12-01
#
# A python script to convert a WHDLoad .lha file to an amiga harddisk file 
# using Hst Imager console.

"""WHDLoad to HDF"""

import os
import platform
import re
import subprocess
import json
import codecs
import unicodedata
import shared


# paths
current_path = os.getcwd()
script_path = os.path.dirname(__file__)
hst_imager_path = shared.get_hst_imager_path(script_path)

# enter whdload lha file
whdload_lha_path = shared.select_file_path('WHDLoad lha')
if not os.path.isfile(whdload_lha_path):
    print('Error: WHDLoad lha file \'{0}\' not found'.format(whdload_lha_path))
    exit(1)

# get whdload lha entries
whdload_lha_entries_json = shared.run_command_capture_output([hst_imager_path, 'fs', 'dir', whdload_lha_path, '--recursive', '--format', 'json'])
whdload_lha_entries_dict = json.loads(whdload_lha_entries_json)

# calculate disk size and get whdload slave paths
disk_size = 0
whdload_slave_paths = []
for entry in whdload_lha_entries_dict['entries']:
    disk_size += entry['size']
    slave_match = re.search(r'\.slave$', entry['name'], re.I)
    if slave_match:
        whdload_slave_paths.append(entry['name'])

# add 10mb extra for amiga os, kickstart and whdload files
disk_size += 10 * 1024 * 1024

# error, if whdload lha file doesn't contain any .slave files
if len(whdload_slave_paths) == 0:
    print('No WHDLoad slave files found in \'{0}\''.format(whdload_lha_path))
    exit(1)

# confirm use amiga os 3.1
use_amigaos_31 = shared.confirm("Use Amiga OS 3.1 adf files", "enter = yes, no = 3.1+/other")

# confirm use pfs3 confirm 
use_pfs3 = shared.confirm("Use PFS3 file system?", "enter = yes, no = DOS3")

# get image path based on selected whdload lha
image_path = os.path.join(current_path, '{0}.vhd'.format(os.path.splitext(os.path.basename(whdload_lha_path))[0]))
print('Creating image file \'{0}\''.format(image_path))

# create blank image of calculated disk size
shared.run_command([hst_imager_path, 'blank', image_path, str(disk_size)])

# initialize rigid disk block for entire disk
shared.run_command([hst_imager_path, 'rdb', 'init', image_path])

if use_pfs3:
    # add rdb file system pfs3aio with dos type PDS3
    shared.run_command([hst_imager_path, 'rdb', 'fs', 'add', image_path, 'pfs3aio', 'PDS3'])

    # add rdb partition of entire disk with device name "DH0" and set bootable
    shared.run_command([hst_imager_path, 'rdb', 'part', 'add', image_path, 'DH0', 'PDS3', '*', '--bootable'])
else:
    # get amigaos install adf path
    amigaos_install_adf_path = shared.get_amigaos_install_adf_path(current_path, use_amigaos_31)
    
    # add rdb file system fast file system with dos type DOS3 imported from amiga os install adf
    shared.run_command([hst_imager_path, 'rdb', 'fs', 'import', image_path, amigaos_install_adf_path, '--dos-type', 'DOS3', '--name', 'FastFileSystem'])

    # add rdb partition of entire disk with device name "DH0" and set bootable
    shared.run_command([hst_imager_path, 'rdb', 'part', 'add', image_path, 'DH0', 'DOS3', '*', '--bootable'])

# format rdb partition number 1 with volume name "WHDLoad"
shared.run_command([hst_imager_path, 'rdb', 'part', 'format', image_path, '1', 'WHDLoad'])

# install minimal amigaos
shared.install_minimal_amigaos(hst_imager_path, image_path, use_amigaos_31)

# install minimal whdload script
shared.install_minimal_whdload(hst_imager_path, image_path)

# extract whdload lha to image file
shared.run_command([hst_imager_path, 'fs', 'mkdir', os.path.join(image_path, 'rdb', 'dh0', 'WHDLoad')])
shared.run_command([hst_imager_path, 'fs', 'extract', whdload_lha_path, os.path.join(image_path, 'rdb', 'dh0', 'WHDLoad'), '--recursive', '--force'])

# create startup sequence
startup_sequence_lines = [
    "C:SetPatch QUIET",
    "C:Version >NIL:",
    "FailAt 21",
    "C:MakeDir RAM:T RAM:Clipboards RAM:ENV RAM:ENV/Sys",
    "C:Assign T: RAM:T"
]

# add start whdload slave. if 1 then run directly, if more show request choice
if len(whdload_slave_paths) == 1:
    whdload_slave_path = whdload_slave_paths[0]
    startup_sequence_lines.append('cd "{0}"'.format(os.path.join('WHDLoad', os.path.dirname(whdload_slave_path)).replace('\\', '/')))
    startup_sequence_lines.append('WHDLoad "{0}" PRELOAD'.format(os.path.basename(whdload_slave_path)))
else:
    options = []
    for whdload_slave_path in whdload_slave_paths:
        options.append(os.path.splitext(os.path.basename(whdload_slave_path))[0])
    startup_sequence_lines.append('set slave `RequestChoice "Start WHDLoad slave" "Select WHDLoad slave to start?" "{0}"`'.format('|'.join(options)))

    option = 1
    for whdload_slave_path in whdload_slave_paths:
        if option == len(whdload_slave_paths):
            option = 0

        startup_sequence_lines.append('IF "$slave" EQ {0} VAL'.format(option))
        startup_sequence_lines.append('  cd "{0}"'.format(os.path.join("WHDLoad", os.path.dirname(whdload_slave_path)).replace('\\', '/')))
        startup_sequence_lines.append('  WHDLoad "{0}" PRELOAD'.format(os.path.basename(whdload_slave_path)))
        startup_sequence_lines.append('  SKIP end')
        startup_sequence_lines.append('ENDIF')

        option = option + 1

    startup_sequence_lines.append('LAB end')

# write startup sequence
startup_sequence_path = os.path.join(script_path, 'Startup-Sequence')
shared.write_text_lines_for_amiga(startup_sequence_path, startup_sequence_lines)

# copy startup sequence to image file
shared.run_command([hst_imager_path, 'fs', 'copy', startup_sequence_path, os.path.join(image_path, 'rdb', 'dh0', 'S'), '--force'])

print('Done')
