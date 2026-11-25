using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Hst.Imager.AvaloniaApp.Models;

/// <summary>
/// Thread-safe sink for detail lines reported by a running task. Lines are queued without posting each line to
/// the ui thread and drained in batches by progress view model.
/// </summary>
public class DetailsLog : IProgress<string>
{
    private readonly ConcurrentQueue<string> _lines = new();

    public void Report(string value)
    {
        _lines.Enqueue(value ?? string.Empty);
    }

    /// <summary>
    /// Drain lines queued since last drain.
    /// </summary>
    public IList<string> Drain()
    {
        var lines = new List<string>();
        while (_lines.TryDequeue(out var line))
        {
            lines.Add(line);
        }

        return lines;
    }
}
