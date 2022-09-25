﻿namespace Hst.Imager.Core.Extensions
{
    using System;
    using Hst.Core.Extensions;
    using Models;

    public static class SizeExtensions
    {
        public static long ResolveSize(this long value, Size size)
        {
            return size.Unit switch
            {
                Unit.Bytes => Convert.ToInt64(size.Value == 0 ? value : size.Value),
                Unit.Percent => value.Percent((int)size.Value).ToSectorSize(),
                _ => throw new ArgumentOutOfRangeException($"Invalid size unit '{size.Unit}'")
            };
        }
    }
}