using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hst.Amiga.FileSystems.Salvage;
using Hst.Amiga.RigidDiskBlocks;
using FfsDoctor = Hst.Amiga.FileSystems.FastFileSystem.Doctor;
using Pfs3Doctor = Hst.Amiga.FileSystems.Pfs3.Doctor;
using SfsDoctor = Hst.Amiga.FileSystems.SmartFileSystem.Doctor;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Callbacks used by file system doctor adapters to report messages, progress and issues.
/// </summary>
internal class FileSystemDoctorCallbacks
{
    public Action<string> Log { get; init; }
    public Action<long, long> Progress { get; init; }
    public Action<RecoveryIssueLevel, string, long?, string, bool> AddIssue { get; init; }
}

/// <summary>
/// Adapter for checking, repairing and salvaging an Amiga file system using doctors from Hst.Amiga.
/// </summary>
internal interface IFileSystemDoctorAdapter
{
    /// <summary>
    /// Short name of file system, e.g. "FFS", "PFS3" or "SFS".
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Check file system and repair errors, if repair is true.
    /// </summary>
    Task Check(RecoveryReport report, bool repair, CancellationToken token);

    /// <summary>
    /// Salvage directories and files to target. File system is never written to.
    /// </summary>
    Task<SalvageResult> Salvage(ISalvageTarget target, SalvageOptions options, CancellationToken token);
}

internal static class FileSystemDoctorAdapterFactory
{
    /// <summary>
    /// Create adapter for partition block dos type. Returns null, if file system is not supported.
    /// </summary>
    public static IFileSystemDoctorAdapter Create(Stream stream, PartitionBlock partitionBlock,
        FileSystemDoctorCallbacks callbacks) =>
        partitionBlock.DosTypeFormatted switch
        {
            "DOS\\0" or "DOS\\1" or "DOS\\2" or "DOS\\3" or "DOS\\4" or "DOS\\5" or "DOS\\6" or "DOS\\7" =>
                new FastFileSystemDoctorAdapter(stream, partitionBlock, callbacks),
            "PFS\\3" or "PDS\\3" => new Pfs3DoctorAdapter(stream, partitionBlock, callbacks),
            "SFS\\0" => new SmartFileSystemDoctorAdapter(stream, partitionBlock, callbacks),
            _ => null
        };

    /// <summary>
    /// Create adapter for adf.
    /// </summary>
    public static IFileSystemDoctorAdapter CreateAdf(Stream stream, FileSystemDoctorCallbacks callbacks) =>
        new FastFileSystemDoctorAdapter(stream, null, callbacks);
}

internal class FastFileSystemDoctorAdapter(Stream stream, PartitionBlock partitionBlock,
    FileSystemDoctorCallbacks callbacks) : IFileSystemDoctorAdapter
{
    public string Name => partitionBlock == null ? "FFS (ADF)" : $"FFS ({partitionBlock.DosTypeFormatted})";

    private FfsDoctor.DoctorVolume OpenVolume()
    {
        var volume = partitionBlock == null
            ? FfsDoctor.FastFileSystemDoctor.OpenAdf(stream)
            : FfsDoctor.FastFileSystemDoctor.OpenVolume(stream, partitionBlock);
        volume.ShowMsg = callbacks.Log;
        return volume;
    }

    public async Task Check(RecoveryReport report, bool repair, CancellationToken token)
    {
        var volume = OpenVolume();
        callbacks.Progress(0, 0);

        var doctor = await FfsDoctor.FastFileSystemDoctor.Run(volume,
            repair ? FfsDoctor.Mode.Repair : FfsDoctor.Mode.Check, token);

        await DoctorIssues.Add(callbacks, repair, ToDoctorIssues(doctor.Issues), async () =>
            ToDoctorIssues((await FfsDoctor.FastFileSystemDoctor.Run(OpenVolume(), FfsDoctor.Mode.Check, token))
                .Issues));

        var stats = doctor.Stats;
        report.DirectoriesFound = stats.Directories;
        report.FilesFound = stats.Files;
        report.AddStat("Directories", stats.Directories);
        report.AddStat("Files", stats.Files);
        report.AddStat("Hard links", stats.HardLinks);
        report.AddStat("Soft links", stats.SoftLinks);
        report.AddStat("Used blocks", stats.UsedBlocks);
        report.AddStat("Free blocks", stats.FreeBlocks);

        report.Aborted |= doctor.Aborted;
        report.Written |= repair && stats.Fixed > 0;
    }

    public Task<SalvageResult> Salvage(ISalvageTarget target, SalvageOptions options, CancellationToken token) =>
        FfsDoctor.FastFileSystemDoctor.Salvage(OpenVolume(), target, options, token);

    private static IList<DoctorIssue> ToDoctorIssues(IEnumerable<FfsDoctor.Issue> issues) =>
        issues.Select(x => new DoctorIssue(x.Type.ToString(), x.Block, x.Path, x.Message, x.Warning, x.Fixed))
            .ToList();
}

internal class SmartFileSystemDoctorAdapter(Stream stream, PartitionBlock partitionBlock,
    FileSystemDoctorCallbacks callbacks) : IFileSystemDoctorAdapter
{
    public string Name => "SFS";

    private SfsDoctor.DoctorVolume OpenVolume()
    {
        var volume = SfsDoctor.SmartFileSystemDoctor.OpenVolume(stream, partitionBlock);
        volume.ShowMsg = callbacks.Log;
        return volume;
    }

    public async Task Check(RecoveryReport report, bool repair, CancellationToken token)
    {
        var volume = OpenVolume();
        callbacks.Progress(0, 0);

        var doctor = await SfsDoctor.SmartFileSystemDoctor.Run(volume,
            repair ? SfsDoctor.Mode.Repair : SfsDoctor.Mode.Check, token);

        await DoctorIssues.Add(callbacks, repair, ToDoctorIssues(doctor.Issues), async () =>
            ToDoctorIssues((await SfsDoctor.SmartFileSystemDoctor.Run(OpenVolume(), SfsDoctor.Mode.Check, token))
                .Issues));

        var stats = doctor.Stats;
        report.DirectoriesFound = stats.Directories;
        report.FilesFound = stats.Files;
        report.AddStat("Directories", stats.Directories);
        report.AddStat("Files", stats.Files);
        report.AddStat("Soft links", stats.SoftLinks);
        report.AddStat("Used blocks", stats.UsedBlocks);
        report.AddStat("Free blocks", stats.FreeBlocks);

        report.Aborted |= doctor.Aborted;
        report.Written |= repair && stats.Fixed > 0;
    }

    public Task<SalvageResult> Salvage(ISalvageTarget target, SalvageOptions options, CancellationToken token) =>
        SfsDoctor.SmartFileSystemDoctor.Salvage(OpenVolume(), target, options, token);

    private static IList<DoctorIssue> ToDoctorIssues(IEnumerable<SfsDoctor.Issue> issues) =>
        issues.Select(x => new DoctorIssue(x.Type.ToString(), x.Block, x.Path, x.Message, x.Warning, x.Fixed))
            .ToList();
}

internal class Pfs3DoctorAdapter(Stream stream, PartitionBlock partitionBlock,
    FileSystemDoctorCallbacks callbacks) : IFileSystemDoctorAdapter
{
    /// <summary>
    /// Maximum number of repair passes, as some errors are only fixed by another repair pass.
    /// </summary>
    private const int MaxRepairPasses = 3;

    public string Name => $"PFS3 ({partitionBlock.DosTypeFormatted})";

    private Pfs3Doctor.Volume OpenVolume(bool reportProgress)
    {
        var volume = Pfs3Doctor.Pfs3Doctor.OpenVolume(stream, partitionBlock);
        var maxByLevel = new Dictionary<int, long>();
        var partitionSize = (long)(partitionBlock.HighCyl - partitionBlock.LowCyl + 1) * partitionBlock.Surfaces *
                            partitionBlock.BlocksPerTrack * 512;
        volume.ShowMsg = callbacks.Log;
        volume.Status = (level, message, max) =>
        {
            maxByLevel[level] = max;
            if (!string.IsNullOrWhiteSpace(message))
            {
                callbacks.Log(message);
            }
        };
        volume.Progress = (level, progress) =>
        {
            // progress is reported relative to max for level, which is scaled to partition size
            if (reportProgress && maxByLevel.TryGetValue(level, out var max) && max > 0)
            {
                callbacks.Progress(Math.Min(progress, max) * partitionSize / max, partitionSize);
            }
        };

        // repair is confirmed before running doctor, so questions are answered ok
        volume.AskUser = (message, _, _) =>
        {
            callbacks.Log($"{message} (answered ok)");
            return true;
        };
        return volume;
    }

    public async Task Check(RecoveryReport report, bool repair, CancellationToken token)
    {
        // check boot and root blocks are displaced, e.g. written at wrong offset
        var rootBlockResult = await Pfs3Doctor.Pfs3Doctor.RestoreRootBlock(stream, partitionBlock, false,
            Pfs3Doctor.Pfs3Doctor.DefaultMaxDisplacementSectors, token);
        foreach (var message in rootBlockResult.Messages)
        {
            callbacks.Log(message);
        }

        if (!rootBlockResult.RootBlockValid)
        {
            if (!rootBlockResult.Found)
            {
                callbacks.AddIssue(RecoveryIssueLevel.Error,
                    "Root block is invalid and no displaced copy of boot and root blocks was found", null, null,
                    false);
            }
            else if (!repair)
            {
                callbacks.AddIssue(RecoveryIssueLevel.Error,
                    $"Root block is invalid, displaced copy found at sector {rootBlockResult.SourceSector} ({rootBlockResult.DisplacedSectors} sectors displaced)",
                    rootBlockResult.SourceSector, null, false);
            }
            else
            {
                var restoreResult = await Pfs3Doctor.Pfs3Doctor.RestoreRootBlock(stream, partitionBlock, true,
                    Pfs3Doctor.Pfs3Doctor.DefaultMaxDisplacementSectors, token);
                foreach (var message in restoreResult.Messages)
                {
                    callbacks.Log(message);
                }

                callbacks.AddIssue(RecoveryIssueLevel.Error,
                    $"Root block is invalid, displaced copy found at sector {restoreResult.SourceSector} ({restoreResult.DisplacedSectors} sectors displaced)",
                    restoreResult.SourceSector, null, restoreResult.Restored);
                report.Written |= restoreResult.Restored;
            }
        }

        if (!rootBlockResult.RootBlockValid && !(repair && rootBlockResult.Found))
        {
            // doctor can't check volume without a valid root block
            return;
        }

        var volume = OpenVolume(true);
        var doctor = await Pfs3Doctor.Pfs3Doctor.Run(volume,
            repair ? Pfs3Doctor.Mode.Repair : Pfs3Doctor.Mode.Check, false, token);

        var stats = doctor.Stats;
        report.DirectoriesFound = stats.NumDirs;
        report.FilesFound = stats.NumFiles;
        report.AddStat("Directories", stats.NumDirs);
        report.AddStat("Files", stats.NumFiles);
        report.AddStat("Hard links", stats.NumHardLink);
        report.AddStat("Soft links", stats.NumSoftLink);
        report.AddStat("Fragmented files", stats.FragmentedFiles);
        report.AddStat("Blocks checked", stats.BlocksChecked);
        report.AddStat("Doctor result", doctor.Result);

        var errorsFound = stats.NumErrors;
        var remainingErrors = errorsFound;
        var written = repair && stats.ErrorsFixed > 0;

        if (repair && errorsFound > 0 && doctor.Result == Pfs3Doctor.Error.None)
        {
            // errors fixed reported by doctor isn't reliable, so repaired volume is checked to find errors left.
            // more repair passes are done, while errors are left and repair is making progress
            for (var pass = 2;; pass++)
            {
                callbacks.Log("Verifying repaired file system");
                var verify = await Pfs3Doctor.Pfs3Doctor.Run(OpenVolume(false), Pfs3Doctor.Mode.Check, false,
                    token);
                var previousErrors = remainingErrors;
                remainingErrors = verify.Stats.NumErrors;
                callbacks.Log($"{remainingErrors} error(s) left after repair");

                if (remainingErrors == 0 || pass > MaxRepairPasses || remainingErrors >= previousErrors ||
                    verify.Result != Pfs3Doctor.Error.None)
                {
                    break;
                }

                callbacks.Log($"Starting repair pass {pass}");
                var repairPass = await Pfs3Doctor.Pfs3Doctor.Run(OpenVolume(true), Pfs3Doctor.Mode.Repair, false,
                    token);
                written |= repairPass.Stats.ErrorsFixed > 0;
            }
        }

        if (errorsFound > 0 || remainingErrors > 0)
        {
            var fixedErrors = repair ? Math.Max(0, errorsFound - remainingErrors) : 0;
            if (fixedErrors > 0)
            {
                callbacks.AddIssue(RecoveryIssueLevel.Error,
                    $"{fixedErrors} error(s) found and fixed, see log for details", null, null, true);
            }

            if (remainingErrors > 0)
            {
                callbacks.AddIssue(RecoveryIssueLevel.Error,
                    $"{remainingErrors} error(s) {(repair ? "left after repair" : "found")}, see log for details",
                    null, null, false);
            }
        }

        switch (doctor.Result)
        {
            case Pfs3Doctor.Error.None:
                break;
            case Pfs3Doctor.Error.Aborted:
                report.Aborted = true;
                break;
            default:
                callbacks.AddIssue(RecoveryIssueLevel.Error, $"Doctor stopped with error '{doctor.Result}'", null,
                    null, false);
                break;
        }

        report.Written |= written;
    }

    public Task<SalvageResult> Salvage(ISalvageTarget target, SalvageOptions options, CancellationToken token) =>
        Pfs3Doctor.Pfs3Doctor.Salvage(OpenVolume(false), target, options, token);
}

/// <summary>
/// Issue found by fast file system or smart file system doctor.
/// </summary>
internal record DoctorIssue(string Type, uint Block, string Path, string Message, bool Warning, bool Fixed);

internal static class DoctorIssues
{
    /// <summary>
    /// Add issues found by doctor to report. Issues fixed reported by doctor isn't always reliable, so when repair
    /// leaves errors not fixed, repaired volume is checked and errors not found by check are fixed. Errors found by
    /// check, which weren't found by repair, are added as not fixed.
    /// </summary>
    public static async Task Add(FileSystemDoctorCallbacks callbacks, bool repair, IList<DoctorIssue> issues,
        Func<Task<IList<DoctorIssue>>> check)
    {
        IList<DoctorIssue> remaining = null;
        if (repair && issues.Any(x => !x.Warning && !x.Fixed))
        {
            callbacks.Log("Verifying repaired file system");
            remaining = (await check()).Where(x => !x.Warning).ToList();
            callbacks.Log($"{remaining.Count} error(s) left after repair");
        }

        foreach (var issue in issues)
        {
            var isFixed = issue.Fixed ||
                          (remaining != null && !issue.Warning && !remaining.Any(x => IsSame(x, issue)));
            callbacks.AddIssue(issue.Warning ? RecoveryIssueLevel.Warning : RecoveryIssueLevel.Error,
                $"{issue.Type}: {issue.Message}", issue.Block, issue.Path, isFixed);
        }

        foreach (var issue in (remaining ?? []).Where(x => !issues.Any(i => IsSame(i, x))))
        {
            callbacks.AddIssue(RecoveryIssueLevel.Error, $"{issue.Type}: {issue.Message}", issue.Block,
                issue.Path, false);
        }
    }

    private static bool IsSame(DoctorIssue a, DoctorIssue b) => a.Type == b.Type && a.Block == b.Block;
}
