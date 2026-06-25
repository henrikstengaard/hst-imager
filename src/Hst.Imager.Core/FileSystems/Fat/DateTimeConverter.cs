namespace Hst.Imager.Core.FileSystems.Fat;

public static class DateTimeConverter
{
    /// <summary>
    /// Converts bytes 16-bit unsigned integer bytes to hour, minute and second time from following layout:
    /// Byte: 1111111100000000
    /// Bit:  7654321076543210
    /// Time: hhhhhhmmmmmsssss
    ///       `----´`---´`---´
    ///        Hour  Min  Sec
    /// </summary>
    /// <param name="data"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    public static (int, int, int) ConvertUInt16BytesToTime(byte[] data, int offset = 0)
    {
        var hour = data[offset + 1] >> 3;
        var minute = (data[offset] >> 5) + ((data[offset + 1] & 0x7) << 3);
        var second = (data[offset] & 0x1f) * 2;
        
        return (hour, minute, second);
    }

    /// <summary>
    /// Converts bytes 16-bit unsigned integer bytes to year, month and day time from following layout:
    /// Byte: 1111111100000000
    /// Bit:  7654321076543210
    /// Date: yyyyyyymmmmddddd
    ///       `-----´`--´`---´
    ///        Year   Mon Day
    /// </summary>
    /// <param name="data"></param>
    /// <param name="offset"></param>
    /// <returns></returns>
    public static (int, int, int) ConvertUInt16BytesToDate(byte[] data, int offset = 0)
    {
        var year = 1980 + (data[offset + 1] >> 1); // uint16 little endian bits 15-9
        var month = ((data[offset + 1] & 0x1) << 3) + (data[offset] >> 5); // uint16 little endian bits 8-5
        var day = data[offset] & 0x1f; // uint16 little endian bits 4-0

        return (year, month, day);
    }
}