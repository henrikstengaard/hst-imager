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
}