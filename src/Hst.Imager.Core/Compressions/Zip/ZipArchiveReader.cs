using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hst.Imager.Core.Compressions.Zip
{
    public class ZipArchiveReader
    {
        /// <summary>
        /// Local file header signature (0x04034b50)
        /// </summary>
        private const uint LocalFileHeaderSignature = 0x04034b50;

        /// <summary>
        /// Central directory file header signature (0x02014b50)
        /// </summary>
        private const uint CentralDirectoryFileHeaderSignature = 0x02014b50;

        /// <summary>
        /// End of central directory file header signature (0x06054b50)
        /// </summary>
        private const uint EndCentralDirectoryFileHeaderSignature = 0x06054b50;

        /// <summary>
        /// Zip64 end of central directory record signature (0x06064b50)
        /// </summary>
        private const uint Zip64EndCentralDirectoryRecordSignature = 0x06064b50;

        /// <summary>
        /// Zip64 end of central directory locator signature (0x07064b50)
        /// </summary>
        private const uint Zip64EndCentralDirectoryLocatorSignature = 0x07064b50;

        /// <summary>
        /// Archive extra data record signature
        /// </summary>
        private const uint ArchiveExtraDataRecordSignature = 0x08064b50;

        /// <summary>
        /// 0x50, 0x4b, 0x07, 0x08
        /// </summary>
        private const uint DataDescriptorSignature = 0x08074b50;

        private const ushort Zip64ExtraField = 0x0001;

        private readonly Stream stream;
        private readonly byte[] signatureBytes;
        private bool endOfZipArchive;

        public ZipArchiveReader(Stream stream)
        {
            this.stream = stream;
            this.signatureBytes = new byte[4];
            this.endOfZipArchive = false;
        }

        public async Task<IZipHeader> Read()
        {
            if (endOfZipArchive || stream.Position >= stream.Length)
            {
                return null;
            }

            var signatureOffset = stream.Position;

            if (await stream.ReadAsync(signatureBytes) != 4)
            {
                throw new IOException();
            }

            uint signature = ReadUInt32(signatureBytes, 0);

            switch (signature)
            {
                case LocalFileHeaderSignature:
                    return await ReadLocalFileHeader(stream, signatureOffset);
                case CentralDirectoryFileHeaderSignature:
                    return await ReadCentralDirectoryFileHeader(stream, signatureOffset);
                case EndCentralDirectoryFileHeaderSignature:
                    endOfZipArchive = true;
                    return await ReadEndOfCentralDirectoryFileHeader(stream, signatureOffset);
                case Zip64EndCentralDirectoryRecordSignature:
                    return await ReadZip64EndOfCentralDirectoryRecord(stream, signatureOffset);
                case Zip64EndCentralDirectoryLocatorSignature:
                    return await ReadZip64EndOfCentralDirectoryLocator(stream, signatureOffset);
                case ArchiveExtraDataRecordSignature:
                    return await ReadArchiveExtraDataRecord(stream, signatureOffset);
                default:
                    var signatureHexValue = string.Join(string.Empty, signatureBytes.Select(x => x.ToString("x2")));
                    throw new IOException($"Unknown signature 0x{signatureHexValue} at offset {signatureOffset}");
            }
        }

        private static async Task<byte[]> ReadBytes(Stream stream, int size)
        {
            var data = new byte[size];

            if (await stream.ReadAsync(data) != size)
            {
                throw new IOException();
            }

            return data;
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            return (ushort)(data[offset] |
                data[offset + 1] << 8);
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return data[offset] |
                (uint)data[offset + 1] << 8 |
                (uint)data[offset + 2] << 16 |
                (uint)data[offset + 3] << 24;
        }

        private static ulong ReadUInt64(byte[] data, int offset)
        {
            return data[offset] |
                (ulong)data[offset + 1] << 8 |
                (ulong)data[offset + 2] << 16 |
                (ulong)data[offset + 3] << 24 |
                (ulong)data[offset + 4] << 32 |
                (ulong)data[offset + 5] << 40 |
                (ulong)data[offset + 6] << 48 |
                (ulong)data[offset + 7] << 56;
        }

        private static DateTime ReadDateTime(ushort fileModificationTime, ushort fileModificationDate)
        {
            var hour = fileModificationTime >> 11 & 0x1f;
            var minute = (fileModificationTime >> 5) & 0x3f;
            var second = fileModificationTime & 0x1f;

            var year = fileModificationDate >> 9 & 0x7f;
            var month = (fileModificationDate >> 5) & 0xf;
            var day = fileModificationDate & 0x1f;

            // AppNote.txt 4.4.6 says "The date and time are encoded in standard MS-DOS format."
            // Some FAT16 documentation confirms the possible values for keeping track of seconds
            // are 0-29, and that value is doubled (so the result is that you only get even-numbered
            // seconds).
            return new DateTime(1980 + year, month, day, hour, minute, second * 2, DateTimeKind.Local);
        }

        private static async Task<LocalFileHeader> ReadLocalFileHeader(Stream stream, long offset)
        {
            //4.3.7  Local file header:

            //local file header signature     4 bytes(0x04034b50)
            //version needed to extract       2 bytes
            //general purpose bit flag        2 bytes
            //compression method              2 bytes
            //last mod file time              2 bytes
            //last mod file date              2 bytes
            //crc - 32                          4 bytes
            //compressed size                 4 bytes
            //uncompressed size               4 bytes
            //file name length                2 bytes
            //extra field length              2 bytes

            //file name(variable size)
            //extra field(variable size)

            //4.3.8  File data

            //Immediately following the local header for a file
            //SHOULD be placed the compressed or stored data for the file.
            //If the file is encrypted, the encryption header for the file
            //SHOULD be placed after the local header and before the file
            //data.The series of[local file header][encryption header]
            //[file data][data descriptor] repeats for each file in the
            //.ZIP archive.

            var localFileHeaderBytes = new byte[26];

            if (await stream.ReadAsync(localFileHeaderBytes) != localFileHeaderBytes.Length)
            {
                throw new IOException();
            }

            var version = ReadUInt16(localFileHeaderBytes, 0);
            var flags = ReadUInt16(localFileHeaderBytes, 2);
            var method = ReadUInt16(localFileHeaderBytes, 4);
            var fileModificationTime = ReadUInt16(localFileHeaderBytes, 6);
            var fileModificationDate = ReadUInt16(localFileHeaderBytes, 8);
            var crc32 = ReadUInt32(localFileHeaderBytes, 10);
            long compressedSize = ReadUInt32(localFileHeaderBytes, 14);
            long uncompressedSize = ReadUInt32(localFileHeaderBytes, 18);
            var fileNameLength = ReadUInt16(localFileHeaderBytes, 22);
            var extraFieldLength = ReadUInt16(localFileHeaderBytes, 24);

            var dataLength = fileNameLength + extraFieldLength;

            var flagBit3 = 1 << 3;
            var flagBit11 = 1 << 11;
            var hasDataDescriptor = (flags & flagBit3) == flagBit3;
            var hasLanguageEncoding = (flags & flagBit11) == flagBit11;
            var isZip64 = compressedSize == 0xffffffff && uncompressedSize == 0xffffffff;

            var fileModificationDateTime = ReadDateTime(fileModificationTime, fileModificationDate);

            var fileName = string.Empty;
            var extraField = new byte[extraFieldLength];

            if (dataLength > 0)
            {
                var dataBytes = new byte[dataLength];
                if (await stream.ReadAsync(dataBytes) != dataBytes.Length)
                {
                    throw new IOException();
                }

                fileName = hasLanguageEncoding
                    ? Encoding.UTF8.GetString(dataBytes, 0, fileNameLength)
                    : Encoding.ASCII.GetString(dataBytes, 0, fileNameLength);
                Array.Copy(dataBytes, fileNameLength, extraField, 0, extraFieldLength);
            }

            var extraFieldIndex = 0;
            while (extraFieldIndex < extraFieldLength)
            {
                ushort headerId = (ushort)(extraField[extraFieldIndex] | 
                    extraField[extraFieldIndex + 1] << 8);

                dataLength = ReadUInt16(extraField, extraFieldIndex + 2);

                switch (headerId)
                {
                    case Zip64ExtraField:
                        isZip64 = true;
                        uncompressedSize = (long)ReadUInt64(extraField, extraFieldIndex + 4);
                        compressedSize = (long)ReadUInt64(extraField, extraFieldIndex + 12);
                        break;
                }

                extraFieldIndex += 4 + dataLength;
            }

            if (compressedSize > 0)
            {
                stream.Seek(compressedSize, SeekOrigin.Current);
            }

            if (hasDataDescriptor)
            {
                var dataDescriptorBytes = await ReadBytes(stream, 4);

                var uInt32Value = ReadUInt32(dataDescriptorBytes, 0);
                var hasDataDescriptorSignature = uInt32Value == DataDescriptorSignature;

                var dataDescriptorOffset = hasDataDescriptorSignature ? 4 : 0;
                var dataDescriptorSize = dataDescriptorOffset + (isZip64 ? 8 : 16);
                dataDescriptorBytes = await ReadBytes(stream, dataDescriptorSize);
                crc32 = hasDataDescriptorSignature ? ReadUInt32(dataDescriptorBytes, 0) : uInt32Value;
                compressedSize = isZip64
                    ? (long)ReadUInt64(dataDescriptorBytes, dataDescriptorOffset + 4)
                    : ReadUInt32(dataDescriptorBytes, dataDescriptorOffset + 4);
                uncompressedSize = isZip64
                    ? (long)ReadUInt64(dataDescriptorBytes, dataDescriptorOffset + 12)
                    : ReadUInt32(dataDescriptorBytes, dataDescriptorOffset + 8);
            }

            return new LocalFileHeader
            {
                Offset = offset,
                Version = version,
                Flags = flags,
                Method = method,
                FileModificationDate = fileModificationDateTime,
                Crc32 = crc32,
                CompressedSize = compressedSize,
                UncompressedSize = uncompressedSize,
                FileName = fileName,
                ExtraField = extraField
            };
        }

        private static async Task<CentralDirectoryFileHeader> ReadCentralDirectoryFileHeader(Stream stream, long offset)
        {
            // 4.3.12  Central directory structure
            //
            // central file header signature   4 bytes(0x02014b50)
            // version made by                 2 bytes
            // version needed to extract       2 bytes
            // general purpose bit flag        2 bytes
            // compression method              2 bytes
            // last mod file time              2 bytes
            // last mod file date              2 bytes
            // crc - 32                          4 bytes
            // compressed size                 4 bytes
            // uncompressed size               4 bytes
            // file name length                2 bytes
            // extra field length              2 bytes
            // file comment length             2 bytes
            // disk number start               2 bytes
            // internal file attributes        2 bytes
            // external file attributes        4 bytes
            // relative offset of local header 4 bytes

            // file name(variable size)
            // extra field(variable size)
            // file comment(variable size)

            var centralDirectoryFileHeaderBytes = new byte[42];

            if (await stream.ReadAsync(centralDirectoryFileHeaderBytes) != centralDirectoryFileHeaderBytes.Length)
            {
                throw new IOException();
            }

            var versionMadeByZip = centralDirectoryFileHeaderBytes[0];
            var hostOs = centralDirectoryFileHeaderBytes[1];
            var version = ReadUInt16(centralDirectoryFileHeaderBytes, 2);
            var flags = ReadUInt16(centralDirectoryFileHeaderBytes, 4);
            var method = ReadUInt16(centralDirectoryFileHeaderBytes, 6);
            var fileModificationTime = ReadUInt16(centralDirectoryFileHeaderBytes, 8);
            var fileModificationDate = ReadUInt16(centralDirectoryFileHeaderBytes, 10);
            var crc32 = ReadUInt32(centralDirectoryFileHeaderBytes, 12);
            var compressedSize = ReadUInt32(centralDirectoryFileHeaderBytes, 16);
            var uncompressedSize = ReadUInt32(centralDirectoryFileHeaderBytes, 20);
            var fileNameLength = ReadUInt16(centralDirectoryFileHeaderBytes, 24);
            var extraFieldLength = ReadUInt16(centralDirectoryFileHeaderBytes, 26);
            var fileCommentLength = ReadUInt16(centralDirectoryFileHeaderBytes, 28);
            var diskNumber = ReadUInt16(centralDirectoryFileHeaderBytes, 30);
            var internalFileAttributes = ReadUInt16(centralDirectoryFileHeaderBytes, 32);
            var externalFileAttributes = ReadUInt32(centralDirectoryFileHeaderBytes, 34);
            var dataOffset = ReadUInt32(centralDirectoryFileHeaderBytes, 38);

            var fileModificationDateTime = ReadDateTime(fileModificationTime, fileModificationDate);

            var dataLength = fileNameLength + extraFieldLength + fileCommentLength;

            var fileName = string.Empty;
            var extraField = new byte[extraFieldLength];
            var fileComment = string.Empty;

            var flagBit11 = 1 << 11;
            var hasLanguageEncoding = (flags & flagBit11) == flagBit11;

            if (dataLength > 0)
            {
                var dataBytes = new byte[dataLength];
                if (await stream.ReadAsync(dataBytes) != dataBytes.Length)
                {
                    throw new IOException();
                }

                fileName = hasLanguageEncoding
                    ? Encoding.UTF8.GetString(dataBytes, 0, fileNameLength)
                    : Encoding.ASCII.GetString(dataBytes, 0, fileNameLength);
                Array.Copy(dataBytes, fileNameLength, extraField, 0, extraFieldLength);
                fileComment = hasLanguageEncoding
                    ? Encoding.UTF8.GetString(dataBytes, fileNameLength + extraFieldLength, fileCommentLength)
                    : Encoding.ASCII.GetString(dataBytes, fileNameLength + extraFieldLength, fileCommentLength);
            }

            return new CentralDirectoryFileHeader
            {
                Offset = offset,
                VersionMadeByZip = versionMadeByZip,
                HostOs = hostOs,
                Version = version,
                Flags = flags,
                Method = method,
                FileModificationDate = fileModificationDateTime,
                Crc32 = crc32,
                CompressedSize = compressedSize,
                UncompressedSize = uncompressedSize,
                DiskNumber = diskNumber,
                InternalFileAttributes = internalFileAttributes,
                ExternalFileAttributes = externalFileAttributes,
                DataOffset = dataOffset,
                FileName = fileName,
                ExtraField = extraField,
                FileComment = fileComment
            };
        }

        private static async Task<EndOfCentralDirectoryFileHeader> ReadEndOfCentralDirectoryFileHeader(Stream stream, long offset)
        {
            // 4.3.16  End of central directory record:
            //
            // end of central dir signature    4 bytes(0x06054b50)
            // number of this disk             2 bytes
            // number of the disk with the
            // start of the central directory  2 bytes
            // total number of entries in the
            // central directory on this disk  2 bytes
            // total number of entries in
            // the central directory           2 bytes
            // size of the central directory   4 bytes
            // offset of start of central
            // directory with respect to
            // the starting disk number        4 bytes
            // .ZIP file comment length        2 bytes
            // .ZIP file comment(variable size)

            var endOfCentralDirectoryFileHeaderBytes = new byte[18];

            if (await stream.ReadAsync(endOfCentralDirectoryFileHeaderBytes) != endOfCentralDirectoryFileHeaderBytes.Length)
            {
                throw new IOException();
            }

            var diskNumber = ReadUInt16(endOfCentralDirectoryFileHeaderBytes, 0);
            var diskCentralStart = ReadUInt16(endOfCentralDirectoryFileHeaderBytes, 2);
            var numberOfCentralsStored = ReadUInt16(endOfCentralDirectoryFileHeaderBytes, 4);
            var totalNumberOfCentralDirectories = ReadUInt16(endOfCentralDirectoryFileHeaderBytes, 6);
            var sizeOfCentralDirectory = ReadUInt32(endOfCentralDirectoryFileHeaderBytes, 8);
            var offsetCentralDirectoryStart = ReadUInt32(endOfCentralDirectoryFileHeaderBytes, 12);
            var commentLength = ReadUInt16(endOfCentralDirectoryFileHeaderBytes, 16);

            var comment = string.Empty;

            if (commentLength > 0)
            {
                var commentBytes = new byte[commentLength];

                if (await stream.ReadAsync(commentBytes) != commentBytes.Length)
                {
                    throw new IOException();
                }
            }

            return new EndOfCentralDirectoryFileHeader
            {
                Offset = offset,
                DiskNumber = diskNumber,
                DiskCentralStart = diskCentralStart,
                NumberOfCentralsStored = numberOfCentralsStored,
                TotalNumberOfCentralDirectories = totalNumberOfCentralDirectories,
                SizeOfCentralDirectory = sizeOfCentralDirectory,
                OffsetCentralDirectoryStart = offsetCentralDirectoryStart,
                Comment = comment
            };
        }

        private static async Task<Zip64EndOfCentralDirectoryRecord> ReadZip64EndOfCentralDirectoryRecord(Stream stream, long offset)
        {
            // 4.3.14  Zip64 end of central directory record

            // zip64 end of central dir
            // signature                       4 bytes(0x06064b50)
            // size of zip64 end of central
            // directory record                8 bytes
            // version made by                 2 bytes
            // version needed to extract       2 bytes
            // number of this disk             4 bytes
            // number of the disk with the
            // start of the central directory  4 bytes
            // total number of entries in the
            // central directory on this disk  8 bytes
            // total number of entries in the
            // central directory               8 bytes
            // size of the central directory   8 bytes
            // offset of start of central
            // directory with respect to
            // the starting disk number        8 bytes
            // zip64 extensible data sector(variable size)

            var zip64EndOfCentralDirectoryBytes = new byte[52];

            if (await stream.ReadAsync(zip64EndOfCentralDirectoryBytes) != zip64EndOfCentralDirectoryBytes.Length)
            {
                throw new IOException();
            }

            var zip64EndOfCentralDirectorySize = ReadUInt64(zip64EndOfCentralDirectoryBytes, 0);
            var versionMadeBy = ReadUInt16(zip64EndOfCentralDirectoryBytes, 8);
            var versionNeededToExtract = ReadUInt16(zip64EndOfCentralDirectoryBytes, 10);
            var diskNumber = ReadUInt32(zip64EndOfCentralDirectoryBytes, 12);
            var diskCentralDirectoryStart = ReadUInt32(zip64EndOfCentralDirectoryBytes, 16);
            var numberOfCentralDirectoriesStored = ReadUInt64(zip64EndOfCentralDirectoryBytes, 20);
            var totalNumberOfCentralDirectories = ReadUInt64(zip64EndOfCentralDirectoryBytes, 28);
            var sizeOfCentralDirectory = ReadUInt64(zip64EndOfCentralDirectoryBytes, 36);
            var offsetCentralDirectoryStart = ReadUInt64(zip64EndOfCentralDirectoryBytes, 42);
            
            // the zip64 end of central directory is 52 bytes without comment and
            // the zip64 end of central directory is 8 bytes
            var commentLength = 52 - 8 - zip64EndOfCentralDirectorySize;

            var comment = string.Empty;

            if (commentLength > 0)
            {
                var commentBytes = new byte[commentLength];

                if (await stream.ReadAsync(commentBytes) != commentBytes.Length)
                {
                    throw new IOException();
                }
            }

            return new Zip64EndOfCentralDirectoryRecord
            {
                Offset = offset,
                VersionMadeBy = versionMadeBy,
                VersionNeededToExtract = versionNeededToExtract,
                DiskNumber = diskNumber,
                DiskCentralDirectoryStart = diskCentralDirectoryStart,
                NumberOfCentralDirectoriesStored = numberOfCentralDirectoriesStored,
                TotalNumberOfCentralDirectories = totalNumberOfCentralDirectories,
                SizeOfCentralDirectory = sizeOfCentralDirectory,
                OffsetCentralDirectoryStart = offsetCentralDirectoryStart,
                Comment = comment
            };
        }

        private static async Task<Zip64EndOfCentralDirectoryLocator> ReadZip64EndOfCentralDirectoryLocator(Stream stream, long offset)
        {
            // 4.3.15 Zip64 end of central directory locator
            //
            // zip64 end of central dir locator
            // signature                       4 bytes(0x07064b50)
            // number of the disk with the
            // start of the zip64 end of
            // central directory               4 bytes
            // relative offset of the zip64
            // end of central directory record 8 bytes
            // total number of disks           4 bytes

            var headerBytes = await ReadBytes(stream, 16);

            var diskStartZip64CentralDirectory = ReadUInt32(headerBytes, 0);
            var startOfZip64EndOfCentralDirector = ReadUInt64(headerBytes, 4);
            var totalNumberOfDisks = ReadUInt32(headerBytes, 12);

            return new Zip64EndOfCentralDirectoryLocator
            {
                Offset = offset,
                DiskStartZip64CentralDirectory = diskStartZip64CentralDirectory,
                StartOfZip64EndOfCentralDirector = startOfZip64EndOfCentralDirector,
                TotalNumberOfDisks = totalNumberOfDisks
            };
        }

        private static async Task<ArchiveExtraDataRecord> ReadArchiveExtraDataRecord(Stream stream, long offset)
        {
            // 4.3.11  Archive extra data record: 

            // archive extra data signature    4 bytes(0x08064b50)
            // extra field length              4 bytes
            // extra field data(variable size)

            var extraFieldLengthBytes = await ReadBytes(stream, 4);
            var extraFieldLength = (int)ReadUInt32(extraFieldLengthBytes, 0);

            var extraField = await ReadBytes(stream, extraFieldLength);

            return new ArchiveExtraDataRecord
            {
                Offset = offset,
                ExtraFieldLength = extraFieldLength,
                ExtraField = extraField
            };
        }
    }
}