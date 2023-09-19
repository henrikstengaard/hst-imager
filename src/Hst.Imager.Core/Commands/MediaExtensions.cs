using System.Threading.Tasks;

namespace Hst.Imager.Core.Commands;

using System;
using Hst.Core;

public static class MediaExtensions
{
    public static Result<T> Then<T>(this Result<T> firstResult, Func<Result<T>> secondResult)
    {
        return firstResult.HasResult()
            ? firstResult
            : secondResult.Invoke();
    }

    public static Task<Result<T>> Then<T>(this Task<Result<T>> firstResult, Func<Task<Result<T>>> secondResult)
    {
        return firstResult.Result.HasResult()
            ? firstResult
            : secondResult.Invoke();
    }

    public static bool HasResult<T>(this Result<T> result)
    {
        return (result.IsSuccess && result.Value != null) || result.IsFaulted;
    }
}