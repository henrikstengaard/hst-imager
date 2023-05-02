namespace Hst.Imager.Core.Commands;

using System;
using Hst.Core;

public static class MediaExtensions
{
    public static Result<T> Then<T>(this Result<T> firstResult, Func<Result<T>> secondResult)
    {
        return firstResult.IsSuccess ? firstResult : secondResult.Invoke();
    }
}