using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Writes a detailed log file for a recovery operation. Each line is flushed, so the log is usable even if the
/// operation crashes.
/// </summary>
public class RecoveryLogWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    private bool _disposed;

    public string Path { get; }

    public RecoveryLogWriter(string path, bool append = false)
    {
        Path = System.IO.Path.GetFullPath(path);

        var dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _writer = new StreamWriter(new FileStream(Path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(false))
        {
            AutoFlush = true
        };
    }

    public void WriteHeader(RecoveryOperation operation, string target, params (string Name, object Value)[] options)
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        WriteRaw("Hst Imager recovery log");
        WriteRaw(new string('=', 23));
        WriteRaw($"Version: {version}");
        WriteRaw($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        WriteRaw($"Operation: {operation}");
        WriteRaw($"Target: {target}");
        foreach (var (name, value) in options)
        {
            WriteRaw($"{name}: {value}");
        }

        WriteRaw(string.Empty);
    }

    public void Write(string level, string message)
    {
        if (message == null)
        {
            return;
        }

        foreach (var line in message.Replace("\r\n", "\n").Split('\n'))
        {
            WriteRaw($"{DateTime.Now:HH:mm:ss.fff} [{level}] {line}");
        }
    }

    public void Debug(string message) => Write("DBG", message);
    public void Information(string message) => Write("INF", message);
    public void Warning(string message) => Write("WRN", message);
    public void Error(string message) => Write("ERR", message);

    public void WriteReport(RecoveryReport report)
    {
        WriteRaw(string.Empty);
        WriteRaw("Summary");
        WriteRaw(new string('-', 7));
        WriteRaw($"Type: {report.TargetType ?? "unknown"}");
        WriteRaw($"Result: {(report.Aborted ? "Aborted" : report.IsValid ? "Valid" : "Errors found")}");
        WriteRaw($"Errors: {report.Errors}");
        WriteRaw($"Warnings: {report.Warnings}");
        WriteRaw($"Fixed: {report.Fixed}");
        if (report.Operation is RecoveryOperation.PartitionTableRepair or RecoveryOperation.FileSystemRepair
            or RecoveryOperation.FullRepair)
        {
            WriteRaw($"Written: {(report.Written ? "Yes" : "No")}");
        }

        if (report.Stats.Count > 0)
        {
            WriteRaw(string.Empty);
            WriteRaw("Statistics");
            WriteRaw(new string('-', 10));
            foreach (var stat in report.Stats)
            {
                WriteRaw($"{stat.Key}: {stat.Value}");
            }
        }

        if (report.Salvage != null)
        {
            WriteRaw(string.Empty);
            WriteRaw("Salvage");
            WriteRaw(new string('-', 7));
            WriteRaw($"Root block found: {(report.Salvage.RootBlockFound ? "Yes" : "No")}");
            WriteRaw($"Directories: {report.Salvage.Directories}");
            WriteRaw($"Files: {report.Salvage.Files}");
            WriteRaw($"Partial files: {report.Salvage.PartialFiles}");
            WriteRaw($"Bytes: {report.Salvage.Bytes}");
            WriteRaw($"Orphaned directories: {report.Salvage.OrphanedDirectories}");
            WriteRaw($"Skipped: {report.Salvage.Skipped}");
            WriteRaw($"Errors: {report.Salvage.Errors}");
        }

        WriteRaw(string.Empty);
        WriteRaw($"Issues ({report.Issues.Count})");
        WriteRaw(new string('-', 6));
        if (report.Issues.Count == 0)
        {
            WriteRaw("No issues found");
        }

        foreach (var issue in report.Issues)
        {
            WriteRaw(issue.ToString());
        }
    }

    /// <summary>
    /// Raised for each line written to log file, e.g. to show log in front-ends while operation is running.
    /// </summary>
    public event EventHandler<string> LineWritten;

    /// <summary>
    /// Write line to log file without timestamp and level.
    /// </summary>
    public void WriteRaw(string line)
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _writer.WriteLine(line);
        }

        LineWritten?.Invoke(this, line);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
