using System;

namespace Hst.Imager.Core.Extensions;

public static class ArrayExtensions
{
    public static void ConvertUInt16ToBytes(this byte[] data, int offset, ushort value)
    {
        var ushortBytes = BitConverter.GetBytes(value);
        Array.Copy(ushortBytes, 0, data, offset, ushortBytes.Length);
    }
    
    public static void ConvertUInt32ToBytes(this byte[] data, int offset, uint value)
    {
        var uintBytes = BitConverter.GetBytes(value);
        Array.Copy(uintBytes, 0, data, offset, uintBytes.Length);
    }

    public static bool HasMagicNumber(this byte[] data, byte[] magicNumberBytes, int offset = 0)
    {
        return MagicBytes.HasMagicNumber(magicNumberBytes, data, offset);
    }
}