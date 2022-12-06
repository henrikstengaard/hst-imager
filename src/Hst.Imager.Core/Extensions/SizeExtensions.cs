namespace Hst.Imager.Core.Extensions
{
    using System;
    using Models;

    public static class SizeExtensions
    {
        public static long ResolveSize(this long value, Size size)
        {
            return size.Unit switch
            {
                Unit.Bytes => Convert.ToInt64(size.Value == 0 ? value : size.Value),
                Unit.Percent => value.Percent(size.Value),
                _ => throw new ArgumentOutOfRangeException($"Invalid size unit '{size.Unit}'")
            };
        }
    }
}