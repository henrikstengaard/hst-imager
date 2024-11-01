# Zip compression

# Headers

Header are stored in little-endian byte order.

## Local file header

| Offset | Bytes | Description |
|--------|-------|-------------|
| 0 | 4 | Local file header signature = 0x04034b50 (PK♥♦ or "PK\3\4") |
| 4 | 2 | Version needed to extract (minimum) |
| 6 | 2 | General purpose bit flag |
| 8 | 2 | Compression method; e.g. none = 0, DEFLATE = 8 (or "\0x08\0x00") |
| 10 | 2 | File last modification time |
| 12 | 2 | File last modification date |
| 14 | 4 | CRC-32 of uncompressed data |
| 18 | 4 | Compressed size (or 0xffffffff for ZIP64) |
| 22 | 4 | Uncompressed size (or 0xffffffff for ZIP64) |
| 26 | 2 | File name length (n) |
| 28 | 2 | Extra field length (m) |
| 30 | n | File name |
| 30 + n | m | Extra field |

## Extra fields

The extra field fields makes the ZIP format extensible where extra metadata can be used for encryption or certain compression algorithms.

The extra field contains a list of following records:

| Bytes | Description     |
|-------|-----------------|
|     2 | Header ID       |
|     2 | Data length (n) |
|     n | Data            |

Common extra fields:

| Header ID | Description                                                   |
| ----------|---------------------------------------------------------------|
| 0x0001    | ZIP64 data descriptor with compressed and uncompressed sizes. |
| 0x5455    | UTC Unix timestamp.                                           |


Flags	General purpose bit flag:
Bit 00: encrypted file
Bit 01: compression option
Bit 02: compression option
Bit 03: data descriptor
Bit 04: enhanced deflation
Bit 05: compressed patched data
Bit 06: strong encryption
Bit 07-10: unused
Bit 11: language encoding
Bit 12: reserved
Bit 13: mask header values
Bit 14-15: reserved

## References

- https://pkwaredownloads.blob.core.windows.net/pkware-general/Documentation/APPNOTE-6.3.9.TXT
- https://users.cs.jmu.edu/buchhofp/forensics/formats/pkzip.html
- https://en.wikipedia.org/wiki/ZIP_(file_format)
- https://www.fileformat.info/format/zip/corion.htm