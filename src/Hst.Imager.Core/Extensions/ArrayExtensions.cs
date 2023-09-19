namespace Hst.Imager.Core.Extensions;

public static class ArrayExtensions
{
    public static bool HasMagicNumber(this byte[] data, byte[] magicNumberBytes, int offset = 0)
    {
        return MagicBytes.HasMagicNumber(magicNumberBytes, data, offset);
    }
}