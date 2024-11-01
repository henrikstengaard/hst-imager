# Ext



## Features

The ext filesystem supports following features based on it's version:

| Feature        | EXT2 | EXT3 | EXT4 |
|----------------|:----:|:----:|:----:|
| ext_attr       |  x   |  x   |  x   |
| resize_inode   |  x   |  x   |  x   |
| dir_index      |  x   |  x   |  x   |
| filetype       |  x   |  x   |  x   |
| sparse_super   |  x   |  x   |  x   |
| has_journal    |      |  x   |  x   |
| needs_recovery |      |  x   |  x   |
| 64bit          |      |      |  x   |
| bigalloc       |      |      |  x   |
| extent         |      |      |  x   |
| flex_bg        |      |      |  x   |
| large_file     |      |      |  x   |
| huge_file      |      |      |  x   |
| meta_bg        |      |      |  x   |
| mmp            |      |      |  x   |
| uninit_bg      |      |      |  x   |
| dir_nlink      |      |      |  x   |
| extra_isize    |      |      |  x   |

uninit_bg (Create a filesystem without initializing all of the block groups. reduced
e2fsck time).

## References

https://ext4.wiki.kernel.org/index.php/Ext4_Disk_Layout

file system kernel support, features support by ext version
https://man7.org/linux/man-pages/man5/ext4.5.html

https://blogs.oracle.com/linux/post/understanding-ext4-disk-layout-part-1

https://www.kernel.org/doc/html/latest/filesystems/ext4/globals.html

https://en.wikipedia.org/wiki/GUID_Partition_Table


https://ext4.wiki.kernel.org/index.php/Ext4_Disk_Layout

https://www.partitionwizard.com/partitionmanager/ext2-vs-ext3-vs-ext4.html

https://github.com/nickdu088/SharpExt4