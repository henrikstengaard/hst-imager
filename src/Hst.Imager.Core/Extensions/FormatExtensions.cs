namespace Hst.Imager.Core.Extensions
{
    using System;

    public static class FormatExtensions
    {
        public static string FormatBytes(this long size, int precision = 1, string format = null)
        {
            var unit = size == 0 ? 0 : Math.Log(size, 1024);
            var units = new[] { "bytes", "KB", "MB", "GB", "TB" };
            var formattedSize = size == 0 ? 0 : Math.Round(Math.Pow(1024, unit - Math.Floor(unit)), precision);
            var formattedUnit = units[Convert.ToInt32(Math.Floor(unit))];

            return string.Concat(string.IsNullOrWhiteSpace(format) ? formattedSize.ToString(format) : formattedSize,
                $" {formattedUnit}");
        }

        public static string FormatElapsed(this TimeSpan value)
        {
            return $"{Convert.ToInt32(Math.Floor(value.TotalHours))}h:{value.Minutes:D2}m:{value.Seconds:D2}s";
        }
    }
}