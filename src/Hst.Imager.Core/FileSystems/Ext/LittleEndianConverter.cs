namespace Hst.Imager.Core.FileSystems.Ext;

public class LittleEndianConverter
{
    public static ushort ConvertBytesToUInt16(byte[] bytes, int offset = 0)
    {
        return (ushort)((bytes[offset] & 0x00ff) | ((bytes[offset + 1] << 8) & 0xff00));
    }

    public static uint ConvertBytesToUInt32(byte[] bytes, int offset = 0)
    {
        return (uint)(bytes[offset] & 0x000000ff) |
               (uint)((bytes[offset + 1] << 8) & 0x0000ff00) |
               (uint)((bytes[offset + 2] << 16) & 0x00ff0000) | 
               (uint)((bytes[offset + 3] << 24) & 0xff000000);
    }
}