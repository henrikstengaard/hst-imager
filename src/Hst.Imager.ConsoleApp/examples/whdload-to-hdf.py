# WHDLoad to HDF
# --------------
#
# Author: Henrik Nørfjand Stengaard
# Date:   2023-04-12
#
# A python script to convert a WHDLoad .lha file to an amiga harddisk file using Hst Imager console.

"""WHDLoad to HDF"""

import os
import stat
import re
import shutil
import subprocess
import json
import codecs
import unicodedata
import urllib.request

# write text lines for amiga
def write_text_lines_for_amiga(path, lines):
    """Write Text Lines for Amiga"""
    with codecs.open(path, "w", "iso-8859-1") as f:
        for l in lines:
            f.write(unicodedata.normalize('NFC', l)+"\n")

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

# use hst imager development app, if present
hst_imager_dev = os.path.join(current_path, 'Hst.Imager.ConsoleApp.exe')
if os.path.isfile(hst_imager_dev):
    hst_imager_path = hst_imager_dev

# error, if hst imager is not found
if not os.path.isfile(hst_imager_path):
    print('Error: Hst Imager file \'{0}\' not found'.format(hst_imager_path))
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

whdload_lha_path = input("Enter path to WHDLoad lha: ")

if not os.path.exists(whdload_lha_path):
    print('Error: WHDLoad lha file \'{0}\' not found'.format(whdload_lha_path))
    exit(1)

# get whdload lha entries
whdload_lha_entries_json = run_command_capture_output([hst_imager_path, 'fs', 'dir', whdload_lha_path, '--recursive', '--format', 'json'])
#print(whdload_lha_entries_json)
whdload_lha_entries_dict = json.loads(whdload_lha_entries_json)

# calculate disk size, get whdload slave paths
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

# show use pfs3 confirm dialog 
use_pfs3 = re.search(r'^(|y|yes)$', input("Use PFS3 file system? (enter = yes):"), re.I)

# get image path based on selected whdload lha
image_path = os.path.join(current_path, '{0}.vhd'.format(os.path.splitext(os.path.basename(whdload_lha_path))[0]))
print('Creating image file \'{0}\''.format(image_path))

# create blank image of calculated disk size
run_command([hst_imager_path, 'blank', image_path, str(disk_size)])

# initialize rigid disk block for entire disk
run_command([hst_imager_path, 'rdb', 'init', image_path])

if use_pfs3:
    # add rdb file system pfs3aio with dos type PDS3
    run_command([hst_imager_path, 'rdb', 'fs', 'add', image_path, 'pfs3aio', 'PDS3'])

    # add rdb partition of entire disk with device name "DH0" and set bootable
    run_command([hst_imager_path, 'rdb', 'part', 'add', image_path, 'DH0', 'PDS3', '*', '--bootable'])
else:
    # add rdb file system fast file system with dos type DOS3 imported from amiga os install adf
    run_command([hst_imager_path, 'rdb', 'fs', 'import', image_path, amiga_os_install_adf_path, '--dos-type', 'DOS3', '--name', 'FastFileSystem'])

    # add rdb partition of entire disk with device name "DH0" and set bootable
    run_command([hst_imager_path, 'rdb', 'part', 'add', image_path, 'DH0', 'DOS3', '*', '--bootable'])

# format rdb partition number 1 with volume name "WHDLoad"
run_command([hst_imager_path, 'rdb', 'part', 'format', image_path, '1', 'WHDLoad'])

# extract amiga os install adf to image file
run_command([hst_imager_path, 'fs', 'extract', amiga_os_install_adf_path, os.path.join(image_path, 'rdb', 'dh0')])

# extract amiga os workbench adf to image file
run_command([hst_imager_path, 'fs', 'extract', amiga_os_workbench_adf_path, os.path.join(image_path, 'rdb', 'dh0')])

# extract whdload lha to image file
run_command([hst_imager_path, 'fs', 'extract', whdload_lha_path, os.path.join(image_path, 'rdb', 'dh0', 'WHDLoad'), '--recursive'])

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
    startup_sequence_lines.append('set slave `RequestChoice "Start WHDLoad slave" "Select WHDLoad slave to start?" "{0}"``"'.format('|'.join(options)))

    option = 1
    for whdload_slave_path in whdload_slave_paths:
        if option == len(whdload_slave_paths):
            option = 0

        startup_sequence_lines.append('IF "$slave" EQ {0} VAL'.format(option))
        startup_sequence_lines.append('  cd "{0}"'.format(os.path.join(os.path.dirname(whdload_slave_path)).replace('\\', '/')))
        startup_sequence_lines.append('  WHDLoad "{0}" PRELOAD'.format(os.path.basename(whdload_slave_path)))
        startup_sequence_lines.append('  SKIP end')
        startup_sequence_lines.append('ENDIF')

        option = option + 1

    startup_sequence_lines.append('LAB end')

# write startup sequence
startup_sequence_path = os.path.join(script_path, 'Startup-Sequence')
write_text_lines_for_amiga(startup_sequence_path, startup_sequence_lines)

# copy startup sequence to image file
run_command([hst_imager_path, 'fs', 'copy', startup_sequence_path, os.path.join(image_path, 'rdb', 'dh0', 'S')])

print('Done')
