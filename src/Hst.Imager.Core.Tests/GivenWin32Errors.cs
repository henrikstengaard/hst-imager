namespace Hst.Imager.Core.Tests
{
    using System;
    using System.ComponentModel;
    using System.IO;
    using Apis;
    using Xunit;

    public class GivenWin32Errors
    {
        [Fact]
        public void WhenGetKnownErrorCodeThenMessageAndReasonAreReturned()
        {
            // arrange
            const int errorCrc = 23;

            // act
            var win32Error = Win32Errors.Get(errorCrc);

            // assert
            Assert.Equal(errorCrc, win32Error.Code);
            Assert.Equal("ERROR_CRC", win32Error.Name);
            Assert.Equal("Data error (cyclic redundancy check).", win32Error.Message);
            Assert.False(string.IsNullOrWhiteSpace(win32Error.Reason));
        }

        [Fact]
        public void WhenGetUnknownErrorCodeThenErrorWithoutReasonIsReturned()
        {
            // arrange
            const int unknownErrorCode = 65000;

            // act
            var win32Error = Win32Errors.Get(unknownErrorCode);

            // assert
            Assert.Equal(unknownErrorCode, win32Error.Code);
            Assert.Null(win32Error.Name);
            Assert.False(string.IsNullOrWhiteSpace(win32Error.Message));
            Assert.Null(win32Error.Reason);
        }

        [Fact]
        public void WhenCreateWin32IoExceptionThenMessageContainsWin32ErrorMessageAndReason()
        {
            // arrange
            const int errorAccessDenied = 5;
            var win32Error = Win32Errors.Get(errorAccessDenied);

            // act
            var exception = new Win32IoException("Failed to write 512 bytes at offset 0", errorAccessDenied);

            // assert
            Assert.Equal(errorAccessDenied, exception.NativeErrorCode);
            Assert.Equal(unchecked((int)0x80070005), exception.HResult);
            Assert.StartsWith("Failed to write 512 bytes at offset 0", exception.Message);
            Assert.Contains("Win32 error 5 (ERROR_ACCESS_DENIED): Access is denied.", exception.Message);
            Assert.Contains($"Reason: {win32Error.Reason}", exception.Message);
        }

        [Fact]
        public void WhenFindWin32ErrorInInnerExceptionThenWin32ErrorIsReturned()
        {
            // arrange
            const int errorNotReady = 21;
            var exception = new IOException("Failed to open physical drive",
                new Win32Exception(errorNotReady, "Failed to open path"));

            // act
            var win32Error = Win32Errors.Find(exception);

            // assert
            Assert.NotNull(win32Error);
            Assert.Equal(errorNotReady, win32Error.Code);
            Assert.Equal("ERROR_NOT_READY", win32Error.Name);
        }

        [Fact]
        public void WhenFindWin32ErrorInAggregateExceptionThenWin32ErrorIsReturned()
        {
            // arrange
            const int errorIoDevice = 1117;
            var exception = new AggregateException(new InvalidOperationException(),
                new Win32IoException("Failed to read", errorIoDevice));

            // act
            var win32Error = Win32Errors.Find(exception);

            // assert
            Assert.NotNull(win32Error);
            Assert.Equal(errorIoDevice, win32Error.Code);
        }

        [Fact]
        public void WhenFindWin32ErrorInExceptionWithoutWin32ErrorThenNullIsReturned()
        {
            // arrange
            var exception = new IOException("Failed", new InvalidOperationException());

            // act
            var win32Error = Win32Errors.Find(exception);

            // assert
            Assert.Null(win32Error);
        }
    }
}
