namespace Hst.Imager.ConsoleApp.Presenters
{
    using System.Linq;
    using System.Text;
    using Core.Commands.RecoveryCommands;
    using Core.Extensions;

    public static class RecoveryPresenter
    {
        public static string PresentReport(RecoveryReport report)
        {
            var outputBuilder = new StringBuilder();

            outputBuilder.AppendLine();
            outputBuilder.AppendLine($"Recovery {report.Operation}:");
            outputBuilder.AppendLine();

            var summaryRows = new[]
                {
                    new[] { "Target", report.Target },
                    new[] { "Type", report.TargetType ?? "Unknown" },
                    new[] { "Result", GetResult(report) },
                    new[] { "Errors", report.Errors.ToString() },
                    new[] { "Warnings", report.Warnings.ToString() },
                    new[] { "Fixed", report.Fixed.ToString() }
                }
                .Concat(report.Operation is RecoveryOperation.PartitionTableRepair or RecoveryOperation.FileSystemRepair
                    ? [["Written", report.Written ? "Yes" : "No"]]
                    : [])
                .Concat(report.Stats.Select(x => new[] { x.Key, x.Value }))
                .Concat([["Log file", report.LogPath]]);

            outputBuilder.Append(TablePresenter.Present(new Table
            {
                Columns = [new Column { Name = "Name" }, new Column { Name = "Value" }],
                Rows = summaryRows.Select(x => new Row { Columns = x }).ToArray()
            }));

            if (report.Salvage != null)
            {
                outputBuilder.AppendLine();
                outputBuilder.AppendLine("Salvage:");
                outputBuilder.AppendLine();
                outputBuilder.Append(TablePresenter.Present(new Table
                {
                    Columns =
                    [
                        new Column { Name = "Directories", Alignment = ColumnAlignment.Right },
                        new Column { Name = "Files", Alignment = ColumnAlignment.Right },
                        new Column { Name = "Partial Files", Alignment = ColumnAlignment.Right },
                        new Column { Name = "Size", Alignment = ColumnAlignment.Right },
                        new Column { Name = "Orphaned Dirs", Alignment = ColumnAlignment.Right },
                        new Column { Name = "Skipped", Alignment = ColumnAlignment.Right },
                        new Column { Name = "Errors", Alignment = ColumnAlignment.Right }
                    ],
                    Rows =
                    [
                        new Row
                        {
                            Columns =
                            [
                                report.Salvage.Directories.ToString(),
                                report.Salvage.Files.ToString(),
                                report.Salvage.PartialFiles.ToString(),
                                report.Salvage.Bytes.FormatBytes(),
                                report.Salvage.OrphanedDirectories.ToString(),
                                report.Salvage.Skipped.ToString(),
                                report.Salvage.Errors.ToString()
                            ]
                        }
                    ]
                }));
            }

            var issues = report.Issues.Where(x => x.Level != RecoveryIssueLevel.Info).ToList();
            if (issues.Count > 0)
            {
                outputBuilder.AppendLine();
                outputBuilder.AppendLine("Issues:");
                outputBuilder.AppendLine();
                outputBuilder.Append(TablePresenter.Present(new Table
                {
                    Columns =
                    [
                        new Column { Name = "Level" },
                        new Column { Name = "Block", Alignment = ColumnAlignment.Right },
                        new Column { Name = "Path" },
                        new Column { Name = "Fixed" },
                        new Column { Name = "Message" }
                    ],
                    Rows = issues.Select(x => new Row
                    {
                        Columns =
                        [
                            x.Level.ToString(),
                            x.Block?.ToString() ?? string.Empty,
                            x.Path ?? string.Empty,
                            x.Fixed ? "Yes" : string.Empty,
                            x.Message
                        ]
                    }).ToArray()
                }));
            }

            outputBuilder.AppendLine();
            outputBuilder.AppendLine($"Detailed log written to '{report.LogPath}'");

            return outputBuilder.ToString();
        }

        private static string GetResult(RecoveryReport report)
        {
            if (report.Aborted)
            {
                return "Aborted";
            }

            if (report.Operation == RecoveryOperation.FileSystemSalvage)
            {
                return report.Errors == 0 ? "Salvaged" : "Salvaged with errors";
            }

            return report.IsValid ? "Valid" : "Errors found";
        }
    }
}
