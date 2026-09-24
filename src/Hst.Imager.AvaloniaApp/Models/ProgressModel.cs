namespace Hst.Imager.AvaloniaApp.Models;

public class ProgressModel
{
    public string Title { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
    public double PercentComplete { get; set; }
    public long? BytesPerSecond { get; set; }
    public long? BytesTotal { get; set; }
    public long? BytesProcessed { get; set; }
    public long? BytesRemaining { get; set; }
    public long? MillisecondsTotal { get; set; }
    public long? MillisecondsElapsed { get; set; }
    public long? MillisecondsRemaining { get; set; }
}
