using System;
using System.Collections.Generic;
using System.Linq;

namespace Hst.Imager.Core.Optimization;

/// <summary>
/// Map of byte ranges known to be free (unused by a file system).
/// All bytes not marked free are considered used.
/// </summary>
public class AllocationMap
{
    private readonly List<ByteRange> _freeRanges = new();
    private bool _isNormalized = true;

    /// <summary>
    /// Mark a byte range as free.
    /// </summary>
    public void AddFree(long offset, long length)
    {
        if (length <= 0)
        {
            return;
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset can't be negative");
        }

        _freeRanges.Add(new ByteRange(offset, length));
        _isNormalized = false;
    }

    /// <summary>
    /// Mark byte ranges as free.
    /// </summary>
    public void AddFree(IEnumerable<ByteRange> ranges)
    {
        foreach (var range in ranges)
        {
            AddFree(range.Offset, range.Length);
        }
    }

    /// <summary>
    /// Sorted and merged free byte ranges.
    /// </summary>
    public IReadOnlyList<ByteRange> FreeRanges
    {
        get
        {
            Normalize();
            return _freeRanges;
        }
    }

    /// <summary>
    /// Total number of free bytes.
    /// </summary>
    public long FreeBytes => FreeRanges.Sum(x => x.Length);

    /// <summary>
    /// Get used byte ranges (ranges not marked free) within offset and length.
    /// </summary>
    public IEnumerable<ByteRange> GetUsedRanges(long offset, long length)
    {
        Normalize();

        var end = offset + length;
        var position = offset;

        for (var i = FindFirstFreeRangeEndingAfter(offset); i < _freeRanges.Count && position < end; i++)
        {
            var freeRange = _freeRanges[i];
            if (freeRange.Offset >= end)
            {
                break;
            }

            if (freeRange.Offset > position)
            {
                yield return new ByteRange(position, freeRange.Offset - position);
            }

            position = Math.Max(position, freeRange.End);
        }

        if (position < end)
        {
            yield return new ByteRange(position, end - position);
        }
    }

    /// <summary>
    /// Find index of first free range, which ends after offset using binary search.
    /// </summary>
    private int FindFirstFreeRangeEndingAfter(long offset)
    {
        var low = 0;
        var high = _freeRanges.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (_freeRanges[middle].End <= offset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private void Normalize()
    {
        if (_isNormalized)
        {
            return;
        }

        _freeRanges.Sort((a, b) => a.Offset.CompareTo(b.Offset));

        var merged = new List<ByteRange>(_freeRanges.Count);
        foreach (var range in _freeRanges)
        {
            if (merged.Count > 0 && merged[^1].End >= range.Offset)
            {
                var last = merged[^1];
                merged[^1] = new ByteRange(last.Offset, Math.Max(last.End, range.End) - last.Offset);
                continue;
            }

            merged.Add(range);
        }

        _freeRanges.Clear();
        _freeRanges.AddRange(merged);
        _isNormalized = true;
    }
}
