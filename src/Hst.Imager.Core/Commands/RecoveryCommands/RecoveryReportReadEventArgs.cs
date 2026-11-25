using System;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

public class RecoveryReportReadEventArgs(RecoveryReport report) : EventArgs
{
    public RecoveryReport Report { get; } = report;
}
