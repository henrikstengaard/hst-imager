namespace HstWbInstaller.Imager.Core.Extensions
{
    using System;

    public static class PercentExtensions
    {
        public static long Percent(this long value, int percent)
        {
            if (percent < 0 || percent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(percent));
            }
            
            return Convert.ToInt64((double)value / 100 * percent);
        }
    }
}