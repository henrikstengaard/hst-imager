namespace Hst.Imager.Core;

public static class MagicBytes
{
    public static readonly byte[]
        VhdMagicNumber = new byte[]
        {
            0x63, 0x6F, 0x6E, 0x65, 0x63, 0x74, 0x69, 0x78
        }; // "conectix" at offset 0 (Windows Virtual PC Virtual Hard Disk file format)

    public static readonly byte[]
        RdbMagicNumber = new byte[] { 0x52, 0x44, 0x53, 0x4B }; // "RDSK" at sector 0-15 (Amiga Rigid Disk Block)

    public static readonly byte[]
        Iso9660MagicNumber = new byte[]
            { 0x43, 0x44, 0x30, 0x30, 0x31 }; // CD001 at offset 0x8001, 0x8801 or 0x9001 (ISO9660 CD/DVD image file)

    public static readonly byte[]
        AdfDosMagicNumber = new byte[] { 0x44, 0x4f, 0x53 }; // "DOS" at offset 0 (Amiga Disk File)

    public static readonly byte[] LhaMagicNumber = new byte[] { 0x2D, 0x6C, 0x68 }; // "-lh" at offset 2 (Lha archive)
    public static readonly byte[] LzxMagicNumber = new byte[] { 0x4C, 0x5A, 0x58 }; // "LZX" at offset 0 (Lzx archive)

    public static readonly byte[] LzwMagicNumber = new byte[] { 0x1f, 0x9d }; // offset 0 (.Z archive)
    
    public static readonly byte[] ZipMagicNumber1 = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // "PK" at offset 0 (Zip archive, normal archive)
    public static readonly byte[] ZipMagicNumber2 = new byte[] { 0x50, 0x4B, 0x05, 0x06 }; // "PK" at offset 0 (Zip archive, empty archive)
    public static readonly byte[] ZipMagicNumber3 = new byte[] { 0x50, 0x4B, 0x07, 0x08 }; // "PK" at offset 0 (Zip archive, spanned archive)
    public static readonly byte[] ZxHeader = new byte[] { 0xfd, 0x37, 0x7a, 0x58, 0x5a, 0 };
    public static readonly byte[] GzHeader = new byte[] { 0x1f, 0x8b };

    public static readonly byte[] RarMagicNumber150 = new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1a, 0x07, 0x0 }; // Roshal ARchive compressed archive v1.50 onwards
    public static readonly byte[] RarMagicNumber500 = new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1a, 0x07, 0x1, 0x0 }; // Roshal ARchive compressed archive v5.00 onwards

    public static bool HasMagicNumber(byte[] magicNumber, byte[] data, int dataOffset)
    {
        for (var i = 0; i < magicNumber.Length && dataOffset + i < data.Length; i++)
        {
            if (magicNumber[i] != data[dataOffset + i])
            {
                return false;
            }
        }

        return true;
    }
}