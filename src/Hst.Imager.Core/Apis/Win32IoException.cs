namespace Hst.Imager.Core.Apis
{
    using System.IO;

    /// <summary>
    /// I/O exception for a failed win32 api call with windows error message and reason describing what could be
    /// causing the error.
    /// </summary>
    public class Win32IoException : IOException
    {
        public Win32Error Win32Error { get; }

        public int NativeErrorCode => Win32Error.Code;

        public Win32IoException(string message, int nativeErrorCode)
            : base(Win32Errors.CreateMessage(message, nativeErrorCode), HResultFromWin32(nativeErrorCode))
        {
            Win32Error = Win32Errors.Get(nativeErrorCode);
        }

        private static int HResultFromWin32(int nativeErrorCode) =>
            nativeErrorCode <= 0 ? nativeErrorCode : unchecked((int)(0x80070000 | (uint)(nativeErrorCode & 0xffff)));
    }
}
