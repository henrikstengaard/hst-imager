namespace Hst.Imager.Core;

public class IoError
{
    public readonly long Offset;
    public readonly int Length;
    public readonly string ErrorMessage;

    public IoError(long offset, int length, string errorMessage)
    {
        Offset = offset;
        Length = length;
        ErrorMessage = errorMessage;
    }
}