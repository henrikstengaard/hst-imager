using System.Linq;

namespace Hst.Imager.Core.MagicBytes;

public static class MagicBytesRegisterExtensions
{
    private static bool HasMagicNumber(byte[] magicNumbers, byte[] data, int offset)
    {
        if (offset >= data.Length)
        {
            return false;
        }
        
        for (var i = 0; i < magicNumbers.Length && offset + i < data.Length; i++)
        {
            if (magicNumbers[i] != data[offset + i])
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryResolve(this MagicBytesRegister register, byte[] data, out DataType dataType)
    {
        var identifier = register.Identifiers.FirstOrDefault(x => HasMagicNumber(x.MagicBytes, data, x.Offset));

        dataType = identifier?.DataType ?? DataType.Unknown;
        
        return identifier != null;
    }
}