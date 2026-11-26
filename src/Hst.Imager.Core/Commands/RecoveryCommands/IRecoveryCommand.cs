using System;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Recovery command writing a log and reporting a recovery report.
/// </summary>
public interface IRecoveryCommand
{
    /// <summary>
    /// Raised with report, when recovery operation is done.
    /// </summary>
    event EventHandler<RecoveryReportReadEventArgs> RecoveryReportRead;

    /// <summary>
    /// Raised for each line written to log file.
    /// </summary>
    event EventHandler<string> LogLineWritten;
}
