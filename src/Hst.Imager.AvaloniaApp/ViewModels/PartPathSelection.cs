using System;
using System.Collections.ObjectModel;
using Hst.Imager.Core.Commands;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

/// <summary>
/// Part of a disk or image file to operate on (disk, partition table, partition or custom start offset).
/// </summary>
public class PartPathSelection : ReactiveObject
{
    private ObservableCollection<SelectOption> _options = [];
    private SelectOption? _selected;
    private long _startOffset;

    /// <summary>
    /// Raised when user selects another part.
    /// </summary>
    public event Action<SelectOption?>? SelectionChanged;

    public ObservableCollection<SelectOption> Options
    {
        get => _options;
        private set
        {
            this.RaiseAndSetIfChanged(ref _options, value);
            this.RaisePropertyChanged(nameof(HasOptions));
        }
    }

    public SelectOption? Selected
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value)) return;
            this.RaiseAndSetIfChanged(ref _selected, value);
            this.RaisePropertyChanged(nameof(IsCustom));
            StartOffset = 0;
            SelectionChanged?.Invoke(value);
        }
    }

    public long StartOffset
    {
        get => _startOffset;
        set => this.RaiseAndSetIfChanged(ref _startOffset, value);
    }

    public bool HasOptions => _options.Count > 0;

    public bool IsCustom => _selected?.Value == MediaOptions.CustomPartPath;

    public void Update(MediaInfo? media, bool includePartitions = true)
    {
        Options = media == null
            ? []
            : new ObservableCollection<SelectOption>(MediaOptions.GetPartPathOptions(media, includePartitions));
        _selected = null;
        Selected = _options.Count > 0 ? _options[0] : null;
    }

    /// <summary>
    /// Path to use for selected part, media path is used for custom part or no part selected.
    /// </summary>
    public string ResolvePath(string mediaPath) =>
        _selected == null || IsCustom ? mediaPath : _selected.Value;

    /// <summary>
    /// Selected part formatted for confirm dialog descriptions.
    /// </summary>
    public string Formatted => _selected == null
        ? string.Empty
        : IsCustom ? $" - Start offset {StartOffset}" : $" - {_selected.Title}";
}
