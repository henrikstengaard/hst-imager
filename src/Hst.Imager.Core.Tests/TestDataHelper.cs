namespace Hst.Imager.Core.Tests;

public static class TestDataHelper
{
    public static byte[] CreateTestData(long size)
    {
        var data = new byte[size];

        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }

        return data;
    }

    public static void ByteSwapData(byte[] data)
    {
        for (var i = 0; i < data.Length - (data.Length % 2); i += 2)
        {
            (data[i], data[i + 1]) = (data[i + 1], data[i]);
        }
    }
}