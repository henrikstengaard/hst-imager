namespace Hst.Imager.Core.Extensions
{
    using System;
    using Hst.Core;
    using Models;

    public static class SizeExtensions
    {
        public static long ResolveSize(this long value, Size size)
        {
            return size.Unit switch
            {
                Unit.Bytes => Convert.ToInt64(size.Value == 0 ? value : Math.Min(value, size.Value)),
                Unit.Percent => value.Percent(size.Value),
                _ => throw new ArgumentOutOfRangeException($"Invalid size unit '{size.Unit}'")
            };
        }

        /// <summary>
        /// Resolve size for source with unknown size, e.g. gzip compressed image. Size in bytes is used as is,
        /// size 0 or 100% returns 0 for unknown size to process until end of source. Other percent sizes
        /// can't be resolved and returns error.
        /// </summary>
        public static Result<long> ResolveUnknownSize(this Size size)
        {
            return size.Unit switch
            {
                Unit.Bytes => new Result<long>(Convert.ToInt64(size.Value)),
                Unit.Percent when size.Value is 0 or 100 => new Result<long>(0),
                Unit.Percent => new Result<long>(new Error(
                    $"Size {size.Value}% can't be used for source with unknown size")),
                _ => throw new ArgumentOutOfRangeException($"Invalid size unit '{size.Unit}'")
            };
        }
    }
}