using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Hst.Imager.AvaloniaApp.Models;
using Hst.Imager.Core.Commands;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

/// <summary>
/// Target to initialize partition table for, either the disk or a PiStorm rigid disk block in a master boot record
/// partition.
/// </summary>
public class InitializeTarget
{
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// PiStorm partition table to initialize. Null for disk.
    /// </summary>
    public DiskPartitionTable? Table { get; init; }

    public long DiskSize { get; init; }

    public bool IsDisk => Table == null;

    public override string ToString() => Title;
}

/// <summary>
/// Initialize partition table dialog to select partition table type and options for initializing rigid disk block.
/// </summary>
public class InitializePartitionTableViewModel : ViewModelBase
{
    private const long MinRdbSize = 10 * 1024 * 1024;

    private static readonly List<SelectOption> AllTableTypeOptions =
    [
        new() { Title = "Master Boot Record", Value = nameof(PartitionTableType.MasterBootRecord) },
        new() { Title = "Guid Partition Table", Value = nameof(PartitionTableType.GuidPartitionTable) },
        new() { Title = "Rigid Disk Block", Value = nameof(PartitionTableType.RigidDiskBlock) }
    ];

    private readonly IReadOnlyList<ReservedArea>? _masterBootRecordPartitions;
    private readonly Func<long, long>? _masterBootRecordFreeSpace;
    private InitializeTarget _selectedTarget;
    private List<SelectOption> _tableTypeOptions = AllTableTypeOptions;
    private SelectOption _selectedTableType;
    private bool _keepMasterBootRecord;
    private decimal _rdbBlockLo;
    private long _rdbSize;

    /// <param name="targets">Disk and PiStorm partition tables, which can be initialized.</param>
    /// <param name="selectedTarget">Target selected by default.</param>
    /// <param name="tableType">Partition table type selected by default.</param>
    /// <param name="masterBootRecordPartitions">Partitions of master boot record of disk, which can be kept when
    /// initializing rigid disk block for a hybrid disk. Null, if disk doesn't have a master boot record.</param>
    /// <param name="masterBootRecordFreeSpace">Free space for partitions in master boot record by size of rigid disk
    /// block initialized keeping master boot record.</param>
    public InitializePartitionTableViewModel(IReadOnlyList<InitializeTarget> targets, InitializeTarget selectedTarget,
        PartitionTableType tableType, IReadOnlyList<ReservedArea>? masterBootRecordPartitions,
        Func<long, long>? masterBootRecordFreeSpace = null)
    {
        Targets = targets;
        _masterBootRecordPartitions = masterBootRecordPartitions;
        _masterBootRecordFreeSpace = masterBootRecordFreeSpace;
        _selectedTarget = selectedTarget;

        // existing or initialized master boot record is kept by default for a hybrid disk
        _keepMasterBootRecord = masterBootRecordPartitions != null;
        _selectedTableType = AllTableTypeOptions.FirstOrDefault(x => x.Value == tableType.ToString()) ??
                             AllTableTypeOptions[0];
        UpdateTableTypeOptions();
        _rdbBlockLo = DefaultRdbBlockLo;
    }

    public IReadOnlyList<InitializeTarget> Targets { get; }

    /// <summary>
    /// Targets are shown, when disk has PiStorm rigid disk blocks, which can be initialized.
    /// </summary>
    public bool HasTargetOptions => Targets.Count > 1;

    public InitializeTarget SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (value == null || ReferenceEquals(_selectedTarget, value)) return;
            this.RaiseAndSetIfChanged(ref _selectedTarget, value);
            UpdateTableTypeOptions();
            _rdbSize = 0;
            RaiseOptionsChanged();
            RdbBlockLo = DefaultRdbBlockLo;
        }
    }

    public List<SelectOption> TableTypeOptions
    {
        get => _tableTypeOptions;
        private set => this.RaiseAndSetIfChanged(ref _tableTypeOptions, value);
    }

    public SelectOption SelectedTableType
    {
        get => _selectedTableType;
        set
        {
            if (value == null) return;
            this.RaiseAndSetIfChanged(ref _selectedTableType, value);
            RaiseOptionsChanged();
        }
    }

    public PartitionTableType TableType => Enum.Parse<PartitionTableType>(_selectedTableType.Value);

    public bool IsRdbTableTypeSelected => TableType == PartitionTableType.RigidDiskBlock;

    /// <summary>
    /// Master boot record can be kept, when initializing rigid disk block for a disk with master boot record.
    /// </summary>
    public bool CanKeepMasterBootRecord => _selectedTarget.IsDisk && _masterBootRecordPartitions != null;

    public bool KeepMasterBootRecord
    {
        get => _keepMasterBootRecord;
        set
        {
            this.RaiseAndSetIfChanged(ref _keepMasterBootRecord, value);
            _rdbSize = 0;
            RaiseOptionsChanged();
        }
    }

    public bool IsKeepingMasterBootRecord => IsRdbTableTypeSelected && _keepMasterBootRecord && CanKeepMasterBootRecord;

    /// <summary>
    /// Sector rigid disk block is written to (0-15). Sector 0 is used by master boot record.
    /// </summary>
    public decimal RdbBlockLo
    {
        get => _rdbBlockLo;
        set
        {
            this.RaiseAndSetIfChanged(ref _rdbBlockLo, Math.Clamp(Math.Round(value), MinRdbBlockLo, 15));
            this.RaisePropertyChanged(nameof(SpaceText));
        }
    }

    public decimal MinRdbBlockLo => IsKeepingMasterBootRecord ? 1 : 0;

    /// <summary>
    /// Default sector for rigid disk block is 2, when disk has master boot record in sector 0. Otherwise 0.
    /// </summary>
    private decimal DefaultRdbBlockLo => CanKeepMasterBootRecord ? 2 : 0;

    /// <summary>
    /// Max size of rigid disk block, which is space before first master boot record partition, when keeping
    /// master boot record.
    /// </summary>
    private long MaxRdbSize => IsKeepingMasterBootRecord
        ? _masterBootRecordPartitions!.Select(x => x.Start).DefaultIfEmpty(_selectedTarget.DiskSize).Min()
        : _selectedTarget.DiskSize;

    /// <summary>
    /// Default size of rigid disk block is half of disk, when keeping master boot record for a hybrid disk, so both
    /// partition tables have space for partitions. Limited to max size. Otherwise max size.
    /// </summary>
    private long DefaultRdbSize => IsKeepingMasterBootRecord
        ? Math.Min(MaxRdbSize, Math.Max(_selectedTarget.DiskSize / 2 / PartitionLayout.MiB * PartitionLayout.MiB,
            Math.Min(MinRdbSize, MaxRdbSize)))
        : MaxRdbSize;

    /// <summary>
    /// Selected size of rigid disk block. Defaults to default size.
    /// </summary>
    private long SelectedRdbSize => _rdbSize == 0 ? DefaultRdbSize : _rdbSize;

    /// <summary>
    /// Size of rigid disk block in MB.
    /// </summary>
    public string RdbSizeText
    {
        get => ((decimal)SelectedRdbSize / (1024 * 1024)).ToString("0.##", CultureInfo.CurrentCulture);
        set
        {
            if ((decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var number) ||
                 decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number)) && number >= 0)
            {
                // size larger than max is kept, when keeping master boot record to show error for missing space
                var bytes = (long)(number * 1024 * 1024);
                _rdbSize = bytes == DefaultRdbSize || (bytes > MaxRdbSize && !IsKeepingMasterBootRecord)
                    ? 0
                    : Math.Max(bytes, Math.Min(MinRdbSize, MaxRdbSize));
            }

            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(SpaceText));
            RaiseErrorChanged();
        }
    }

    public string RdbSizeHelpText => IsKeepingMasterBootRecord
        ? $"Max size {MediaOptions.FormatBytes(MaxRdbSize)}, which is space before first Master Boot Record partition."
        : $"Max size {MediaOptions.FormatBytes(MaxRdbSize)}.";

    /// <summary>
    /// Size of rigid disk block to initialize. 0 uses whole disk. Rigid disk block must end before first master boot
    /// record partition, when keeping master boot record.
    /// </summary>
    public long RdbSize => _rdbSize == 0
        ? IsKeepingMasterBootRecord ? DefaultRdbSize : 0
        : Math.Min(_rdbSize, MaxRdbSize);

    /// <summary>
    /// Space available for partitions in rigid disk block and master boot record, when keeping master boot record for
    /// a hybrid disk. Empty otherwise or if size is invalid.
    /// </summary>
    public string SpaceText
    {
        get
        {
            if (!IsKeepingMasterBootRecord || HasError || _masterBootRecordFreeSpace == null)
                return string.Empty;

            var rdbFreeSpace = PartitionLayout.CreateNewRdb(_selectedTarget.DiskSize, RdbSize, (int)_rdbBlockLo,
                _masterBootRecordPartitions).FreeSpace;
            var mbrFreeSpace = _masterBootRecordFreeSpace(RdbSize);
            return $"Rigid Disk Block has {MediaOptions.FormatBytes(rdbFreeSpace)} available for partitions and Master Boot Record has {MediaOptions.FormatBytes(mbrFreeSpace)} available for partitions.";
        }
    }

    public bool HasSpaceText => !string.IsNullOrEmpty(SpaceText);

    /// <summary>
    /// Description of what is erased by initializing partition table.
    /// </summary>
    public string WarningText => !_selectedTarget.IsDisk
        ? $"Initializing erases all partitions in {_selectedTarget.Table!.ContainerName}."
        : IsKeepingMasterBootRecord
            ? "Initializing Rigid Disk Block keeps Master Boot Record and its partitions to create a hybrid disk. Existing Rigid Disk Block and its partitions are erased."
            : "Initializing erases all partition tables and partitions on the disk.";

    /// <summary>
    /// Rigid disk block only requires free space for selected size before first master boot record partition, when
    /// keeping master boot record.
    /// </summary>
    public string ErrorMessage => !IsKeepingMasterBootRecord
        ? string.Empty
        : MaxRdbSize <= 0
            ? "There's no free space for Rigid Disk Block before first Master Boot Record partition. Delete Master Boot Record partitions at start of disk first."
            : SelectedRdbSize > MaxRdbSize
                ? $"Rigid Disk Block of {MediaOptions.FormatBytes(SelectedRdbSize)} requires {MediaOptions.FormatBytes(SelectedRdbSize)} free space before first Master Boot Record partition, but there's only {MediaOptions.FormatBytes(MaxRdbSize)}. Reduce Rigid Disk Block size or delete Master Boot Record partitions at start of disk first."
                : string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool CanInitialize => !HasError;

    /// <summary>
    /// PiStorm disks can only be initialized with rigid disk block.
    /// </summary>
    private void UpdateTableTypeOptions()
    {
        TableTypeOptions = _selectedTarget.IsDisk
            ? AllTableTypeOptions
            : AllTableTypeOptions.Where(x => x.Value == nameof(PartitionTableType.RigidDiskBlock)).ToList();
        if (!TableTypeOptions.Contains(_selectedTableType))
            SelectedTableType = TableTypeOptions[0];
    }

    private void RaiseOptionsChanged()
    {
        this.RaisePropertyChanged(nameof(IsRdbTableTypeSelected));
        this.RaisePropertyChanged(nameof(CanKeepMasterBootRecord));
        this.RaisePropertyChanged(nameof(MinRdbBlockLo));
        if (_rdbBlockLo < MinRdbBlockLo)
            RdbBlockLo = DefaultRdbBlockLo;
        this.RaisePropertyChanged(nameof(RdbSizeText));
        this.RaisePropertyChanged(nameof(RdbSizeHelpText));
        this.RaisePropertyChanged(nameof(WarningText));
        this.RaisePropertyChanged(nameof(SpaceText));
        RaiseErrorChanged();
    }

    private void RaiseErrorChanged()
    {
        this.RaisePropertyChanged(nameof(ErrorMessage));
        this.RaisePropertyChanged(nameof(HasError));
        this.RaisePropertyChanged(nameof(CanInitialize));
        this.RaisePropertyChanged(nameof(HasSpaceText));
    }
}
