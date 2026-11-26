namespace Hst.Imager.AvaloniaApp.Models;

/// <summary>
/// Result of a task shown when task completed successfully.
/// </summary>
/// <param name="Message">Summary of result, e.g. counts found one per line. Null for no summary.</param>
/// <param name="Status">Total status, e.g. "Partition table valid". Null shows completed successfully.</param>
/// <param name="IsWarning">Status is a warning, e.g. errors found or not repaired.</param>
public record ProgressResult(string? Message, string? Status = null, bool IsWarning = false);
