using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Hst.Imager.Core.MagicBytes;

public class MagicBytesRegister
{
    private readonly List<Identifier> identifiers;
    private static readonly Lazy<MagicBytesRegister> SingletonInstance =
        new Lazy<MagicBytesRegister>(
            () =>
            {
                var register = new MagicBytesRegister();
                register.AddDefault();
                return register;
            }, LazyThreadSafetyMode.PublicationOnly);

    public MagicBytesRegister()
    {
        identifiers = new List<Identifier>();
    }

    public IReadOnlyList<Identifier> Identifiers => identifiers;
    
    public static MagicBytesRegister Instance => SingletonInstance.Value;
    
    /// <summary>
    /// Add default magic bytes to the registers list of identifiers.
    /// </summary>
    public void AddDefault()
    {
        identifiers.Add(new Identifier(0, KnownMagicBytes.VhdMagicBytes, DataType.Vhd));

        for (var sector = 0; sector <= 15; sector++)
        {
            identifiers.Add(new Identifier(sector * 512, KnownMagicBytes.RdbMagicBytes, DataType.Rdb));
        }
        
        identifiers.Add(new Identifier(0x8001, KnownMagicBytes.Iso9660MagicBytes, DataType.Iso9660));
        identifiers.Add(new Identifier(0x8801, KnownMagicBytes.Iso9660MagicBytes, DataType.Iso9660));
        identifiers.Add(new Identifier(0x9001, KnownMagicBytes.Iso9660MagicBytes, DataType.Iso9660));

        for (byte dos = 0; dos <= 7; dos++)
        {
            identifiers.Add(new Identifier(0, KnownMagicBytes.AdfDosMagicBytes.Concat([dos]).ToArray(), DataType.Adf));
        }
        
        identifiers.Add(new Identifier(0x1fe, KnownMagicBytes.MbrMagicNumbers, DataType.Mbr));
        
        identifiers.Add(new Identifier(0x200, KnownMagicBytes.GptMagicNumbers, DataType.Gpt));
        
        identifiers.Add(new Identifier(2, KnownMagicBytes.LhaMagicNumbers, DataType.Lha));
        
        identifiers.Add(new Identifier(0, KnownMagicBytes.LzxMagicNumbers, DataType.Lzx));
        
        identifiers.Add(new Identifier(0, KnownMagicBytes.HunkMagicNumbers, DataType.Hunk));
        
        identifiers.Add(new Identifier(0, KnownMagicBytes.LzwMagicNumbers, DataType.Lzw));
        
        identifiers.Add(new Identifier(0, KnownMagicBytes.ZipMagicNumber1, DataType.Zip));
        identifiers.Add(new Identifier(0, KnownMagicBytes.ZipMagicNumber2, DataType.Zip));
        identifiers.Add(new Identifier(0, KnownMagicBytes.ZipMagicNumber3, DataType.Zip));
        
        identifiers.Add(new Identifier(0, KnownMagicBytes.ZxHeader, DataType.Xz));

        identifiers.Add(new Identifier(0, KnownMagicBytes.GzHeader, DataType.Gzip));
        
        identifiers.Add(new Identifier(0, KnownMagicBytes.RarMagicNumber150, DataType.Rar));
        identifiers.Add(new Identifier(0, KnownMagicBytes.RarMagicNumber500, DataType.Rar));
    }
}