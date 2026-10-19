namespace Hst.Imager.Core.Apis
{
    /// <summary>
    /// Win32 error code with windows error message and a reason describing what could be causing the error.
    /// </summary>
    public class Win32Error
    {
        /// <summary>
        /// Win32 error code, e.g. 5.
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// Win32 error name, e.g. ERROR_ACCESS_DENIED.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Windows error message, e.g. Access is denied.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Reason describing what could be causing the error.
        /// </summary>
        public string Reason { get; set; }

        public override string ToString()
        {
            var error = string.IsNullOrWhiteSpace(Name)
                ? $"Win32 error {Code}: {Message}"
                : $"Win32 error {Code} ({Name}): {Message}";

            return string.IsNullOrWhiteSpace(Reason)
                ? error
                : $"{error}{System.Environment.NewLine}Reason: {Reason}";
        }
    }
}
