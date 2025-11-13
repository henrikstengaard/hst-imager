namespace Hst.Imager.ConsoleApp
{
    using System;
    using System.Globalization;

    [AttributeUsage(AttributeTargets.Assembly)]
    internal class BuildDateAttribute(string value) : Attribute
    {
        public DateTime DateTime { get; } = DateTime.ParseExact(value, "yyyyMMddHHmmss",
            CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)
            .ToLocalTime();
    }
}