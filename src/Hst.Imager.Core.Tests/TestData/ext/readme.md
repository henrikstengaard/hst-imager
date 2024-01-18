# Ext test image files

This directory contains Ext2, Ext3 and Ext4 test image files created with the following commands:
```
fallocate --length 1mb ext2.img
mkfs.ext2 ./ext2.img
    
fallocate --length 3mb ext3.img
mkfs.ext3 ./ext3.img

fallocate --length 3mb ext4.img
mkfs.ext4 ./ext4.img    
```

Details can be shown for each image file with the command:
```
dumpe2fs -h ext2.img
```
