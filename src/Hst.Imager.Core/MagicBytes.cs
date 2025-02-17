namespace Hst.Imager.Core;

public static class MagicBytes
{
    /// <summary>
    /// "conectix" at offset 0 (Windows Virtual PC Virtual Hard Disk file format)
    /// </summary>
    public static readonly byte[] VhdMagicNumber = [0x63, 0x6F, 0x6E, 0x65, 0x63, 0x74, 0x69, 0x78];

    /// <summary>
    /// "RDSK" at sector 0-15 (Amiga Rigid Disk Block)
    /// </summary>
    public static readonly byte[] RdbMagicNumber = [0x52, 0x44, 0x53, 0x4B];

    /// <summary>
    /// CD001 at offset 0x8001, 0x8801 or 0x9001 (ISO9660 CD/DVD image file)
    /// </summary>
    public static readonly byte[] Iso9660MagicNumber = [0x43, 0x44, 0x30, 0x30, 0x31];

    /// <summary>
    /// "DOS" at offset 0 (Amiga Disk File)
    /// </summary>
    public static readonly byte[] AdfDosMagicNumber = [0x44, 0x4f, 0x53];

    /// <summary>
    /// MBR boot signature at offset 510 / 0x1fe (Master Boot Record)
    /// </summary>
    public static readonly byte[] MbrMagicNumber = [0x55, 0xaa];

    /// <summary>
    /// "-lh" at offset 2 (Lha archive)
    /// </summary>
    public static readonly byte[] LhaMagicNumber = [0x2D, 0x6C, 0x68];
    
    /// <summary>
    /// "LZX" at offset 0 (Lzx archive)
    /// </summary>
    public static readonly byte[] LzxMagicNumber = [0x4C, 0x5A, 0x58]; 

    /// <summary>
    /// Amiga Hunk header at offset 0
    /// </summary>
    public static readonly byte[] HunkMagicNumber = [0x0, 0x0, 0x03, 0xf3]; 

    /// <summary>
    /// Lzw compression header offset 0 (.Z archive)
    /// </summary>
    public static readonly byte[] LzwMagicNumber = [0x1f, 0x9d];
    
    /// <summary>
    /// "PK" at offset 0 (Zip archive, normal archive)
    /// </summary>
    public static readonly byte[] ZipMagicNumber1 = [0x50, 0x4B, 0x03, 0x04];
    
    /// <summary>
    /// "PK" at offset 0 (Zip archive, empty archive)
    /// </summary>
    public static readonly byte[] ZipMagicNumber2 = [0x50, 0x4B, 0x05, 0x06];
    
    /// <summary>
    /// "PK" at offset 0 (Zip archive, spanned archive)
    /// </summary>
    public static readonly byte[] ZipMagicNumber3 = [0x50, 0x4B, 0x07, 0x08];
    
    public static readonly byte[] ZxHeader = [0xfd, 0x37, 0x7a, 0x58, 0x5a, 0];
    public static readonly byte[] GzHeader = [0x1f, 0x8b];

    /// <summary>
    /// Roshal ARchive compressed archive v1.50 onwards
    /// </summary>
    public static readonly byte[] RarMagicNumber150 = [0x52, 0x61, 0x72, 0x21, 0x1a, 0x07, 0x0];
    
    /// <summary>
    /// Roshal ARchive compressed archive v5.00 onwards
    /// </summary>
    public static readonly byte[] RarMagicNumber500 = [0x52, 0x61, 0x72, 0x21, 0x1a, 0x07, 0x1, 0x0]; 

    public static bool HasMagicNumber(byte[] magicNumber, byte[] data, int dataOffset)
    {
        if (dataOffset >= data.Length)
        {
            return false;
        }
        
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