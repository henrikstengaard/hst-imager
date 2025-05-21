# FAT32 structure

## File Allocation Tables

The data area must start on a cluster boundary. The FAT is a sequence of numbers, each in 32-bit for FAT32 and 16-bit for FAT16.
The k-th number (k ≥ 0) is corresponding to the k-th cluster in the data area, which tells which is the next cluster of the data block.
The first data cluster is cluster number 2. Thus in FAT, the value of:
- 0xFFFFFFFF: Volume id.
- 0x0FFFFFF8-0x0FFFFFFF: End of a cluster chain.
- 0x0FFFFFF7: Bad cluster.
- 0x0FFFFFF0-0x0FFFFFF6 and 0x00000001: Reserved value, shall not be used in FAT.
- 0x00000000: Unused cluster.
- Other value (highest 4-bit must be zero) indicates the position of the next cluster of a file.

Since first data cluster is cluster 2, the first 2 FAT entries are always 0x0FFFFFFF and never allocated to user data.

## Directory table

In FAT32 files are organized in directories. Each directory consists of one or more clusters with a number of 32-byte entries. The 32-byte entries contain Short-File-Name (SFN) 8.3-filename entries or Long-File-Name (LFB) entries.

A SFN 8.3-filename entry has the following format:

| Offset | Length | Description                                              |
|--------|--------|----------------------------------------------------------|
| 0x0    | 8      | File name                                                |
| 0x8    | 3      | Extension                                                |
| 0xB    | 1      | Attribute                                                |
| 0xC    | 1      | Null byte                                                |
| 0xD    | 1      | Creation time, 10 ms portion in 2 sec, value of 0 to 199 |
| 0xE    | 2      | Creation time                                            |
| 0x10   | 2      | Creation date                                            |
| 0x12   | 2      | Last accessed date                                       |
| 0x14   | 2      | High bits of cluster number                              |
| 0x16   | 2      | Time                                                     |
| 0x18   | 2      | Date                                                     |
| 0x1A   | 2      | Lower bits of cluster number                             |
| 0x1C   | 4      | File size                                                |

The attribute is encoded in the following format:
| Bit | Function     | Description                        |
|-----|--------------|------------------------------------|
| 0   | Read-only    | File is read-only                  |
| 1   | Hidden       | File is hidden                     |
| 2   | System       | File is a system file              |
| 3   | Volume label | File is a volume label             |
| 4   | Directory    | File is a directory                |
| 5   | Archive      | Has been changed since last backup |
| 6-7 | Unused       | Not used, zero                     |

The attribute is used to determine if the entry is file, directory or long filename data.

LFN entries has attribute set to 0xF, which is the four least significant bits (read-only, hidden, system, volume label).

The LFN attribute is defined as follows:

The 16-bit time field is encoded in the following format:
- Bit 15-11: Hour (0 to 23)
- Bit 10-5: Minutes (0 to 59)
- Bit 4-0: Seconds divided by 2 (0 to 29)

The 16-bit date field is encoded in the following format:
- Bit 15-9: Year, 1980+n, (n from 0 to 127)
- Bit 8-5: Month (1 to 12)
- Bit 4-0: Day (1 to 31)

A LFN entry has the following format:

| Offset | Length | Description                  |
|--------|--------|------------------------------|
| 0x0    | 1      | Order of LFN entry, >= 1     |
| 0x1    | 10     | Unicode character 1-5        |
| 0xB    | 1      | Attribute                    |
| 0xC    | 1      | Null byte                    |
| 0xD    | 1      | Checksum of the 8.3 filename |
| 0xE    | 12     | Unicode character 6-11       |
| 0x1A   | 2      | Null bytes                   |
| 0x1C   | 4      | Unicode character 12-13      |

The checksum is computed using the 8.3 filename, total of 11 bytes. The C code is as follows:
```
unsigned char compute_checksum(unsigned char filename[11]) {
  unsigned char cksum = filename[0];
  int i;
  for (i=1; i<=11; i++) {
    cksum = ((cksum >> 1)|((cksum & 0x01)<<7)) + filename[i];
  };
  return cksum;
};
```

If the first byte in the 32-byte record is 0xE5, it marks the end of directory record

If the first byte in the 32-byte record is 0x00, it means the record is deleted and can be skipped.

## References
- https://elm-chan.org/docs/fat_e.html
- https://www.adrian.idv.hk/2009-11-15-fat32/
- https://www.pjrc.com/tech/8051/ide/fat32.html