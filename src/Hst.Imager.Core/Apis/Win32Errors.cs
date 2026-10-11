namespace Hst.Imager.Core.Apis
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using Helpers;

    /// <summary>
    /// Win32 errors with windows error messages and reasons loaded from embedded win32-errors.json.
    /// </summary>
    public static class Win32Errors
    {
        private static readonly Lazy<IDictionary<int, Win32Error>> LazyIndex =
            new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

        private static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Get win32 error for error code. Error codes not present in win32-errors.json returns the
        /// windows error message without reason.
        /// </summary>
        /// <param name="code">Win32 error code.</param>
        /// <returns>Win32 error.</returns>
        public static Win32Error Get(int code)
        {
            if (LazyIndex.Value.TryGetValue(code, out var win32Error))
            {
                return win32Error;
            }

            return new Win32Error
            {
                Code = code,
                Message = new Win32Exception(code).Message
            };
        }

        /// <summary>
        /// Find win32 error in exception or its inner exceptions.
        /// </summary>
        /// <param name="exception">Exception to find win32 error in.</param>
        /// <returns>Win32 error, if found. Otherwise null.</returns>
        public static Win32Error Find(Exception exception)
        {
            while (exception != null)
            {
                switch (exception)
                {
                    case Win32IoException win32IoException:
                        return win32IoException.Win32Error;
                    case Win32Exception win32Exception:
                        return Get(win32Exception.NativeErrorCode);
                    case AggregateException aggregateException:
                        return aggregateException.InnerExceptions.Select(Find).FirstOrDefault(x => x != null);
                }

                exception = exception.InnerException;
            }

            return null;
        }

        /// <summary>
        /// Create message with win32 error message and reason appended.
        /// </summary>
        /// <param name="message">Message describing the operation that failed.</param>
        /// <param name="code">Win32 error code.</param>
        /// <returns>Message with win32 error message and reason.</returns>
        public static string CreateMessage(string message, int code) =>
            $"{message}{Environment.NewLine}{Get(code)}";

        private static IDictionary<int, Win32Error> Load()
        {
            using var stream = EmbeddedResourceHelper.GetEmbeddedResourceStream(typeof(Win32Errors).Assembly,
                "win32-errors.json");
            if (stream == null)
            {
                return new Dictionary<int, Win32Error>();
            }

            using var reader = new StreamReader(stream);
            var win32Errors = JsonSerializer.Deserialize<List<Win32Error>>(reader.ReadToEnd(), JsonSerializerOptions) ??
                              [];

            return win32Errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.First());
        }
    }
}
