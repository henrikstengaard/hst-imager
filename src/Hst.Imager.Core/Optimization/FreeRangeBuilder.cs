using System.Collections.Generic;

namespace Hst.Imager.Core.Optimization;

/// <summary>
/// Builds a list of free byte ranges by adding free blocks, merging adjacent blocks into one range.
/// </summary>
public class FreeRangeBuilder
{
    private readonly List<ByteRange> _ranges = new();
    private long _currentOffset = -1;
    private long _currentLength;

    /// <summary>
    /// Add free range. Ranges must be added in ascending order to be merged.
    /// </summary>
    public void Add(long offset, long length)
    {
        if (length <= 0)
        {
            return;
        }

        if (_currentOffset >= 0 && _currentOffset + _currentLength == offset)
        {
            _currentLength += length;
            return;
        }

        Flush();
        _currentOffset = offset;
        _currentLength = length;
    }

    public IReadOnlyList<ByteRange> Build()
    {
        Flush();
        return _ranges;
    }

    private void Flush()
    {
        if (_currentOffset < 0)
        {
            return;
        }

        _ranges.Add(new ByteRange(_currentOffset, _currentLength));
        _currentOffset = -1;
        _currentLength = 0;
    }
}
