# Sparse files

A sparse file is a type of file that allows for efficient storage of data by only allocating disk space for the non-empty parts of the file. This can be particularly useful when dealing with large files that contain a lot of empty or zero-filled data.

## Windows

A new file can be created using the `fsutil` command:
```
fsutil file createnew file.img 0
```

The file is marked as sparse setting the sparse attribute flag for the file:
```
fsutil sparse setflag file.img
```

The file size can then be changed to 512 MB without actually allocating the disk space for the file:
```
fsutil file seteof file.img 512000000
file.img eof set
```

To confirm that the file is marked as sparse, use:
```
fsutil sparse queryflag file.img
This file is set as sparse
```

The `fsutil file queryextents` command is used to query the physical disk extents (clusters/sectors) occupied by a file:
```
fsutil file queryextents file.img
VCN: 0x0        Clusters: 0x1e850    LCN: 0xffffffffffffffff
```

It’s mainly useful for low-level disk analysis, defragmentation tools, or forensic purposes. Output from query extents means the following:
- VCN: Virtual Cluster Number (logical cluster index in the file).
- Clusters: Number of clusters in this extent.
- LCN: Logical Cluster Number (physical cluster index on disk).

Sparse files are often fragmented and will list multiple extents.

## MacOS & Linux

The `truncate` utility can be used to create a sparse file of 512 MiB with the following command:
```
truncate -s 512M file.img
```

It's also possible to create a sparse file using the `dd` command:
```
dd if=/dev/zero of=file.img bs=1 count=0 seek=512M
```

The actual size on disk of the sparse file can be checked using the du command:
```
du -h file.img
0       file.img
```