using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Services;
using ReactiveUI;

namespace Hst.Imager.AvaloniaApp.ViewModels;

public class TransferViewModel : ViewModelBase
{
    private readonly IMediaService _mediaService;
    private readonly IImagingService _imagingService;
    private readonly IDialogService _dialogService;

    private ObservableCollection<MediaItemViewModel> _sourceMediaItems = [];
    private ObservableCollection<MediaItemViewModel> _destMediaItems = [];
    private MediaItemViewModel? _sourceMedia;
    private MediaItemViewModel? _destMedia;
    private long _srcStartOffset;
    private long _destStartOffset;
    private long _size;
    private bool _byteswap;
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private CancellationTokenSource? _cts;

    public TransferViewModel(IMediaService mediaService, IImagingService imagingService, IDialogService dialogService)
    {
        _mediaService = mediaService;
        _imagingService = imagingService;
        _dialogService = dialogService;

        Progress = new ProgressViewModel();

        RefreshMediaCommand = ReactiveCommand.CreateFromTask(RefreshMediaAsync);
        StartTransferCommand = ReactiveCommand.CreateFromTask(StartTransferAsync,
            this.WhenAnyValue(x => x.SourceMedia, x => x.DestMedia, x => x.Progress.IsRunning,
                (src, dst, running) => src != null && dst != null && src.Path != dst.Path && !running));
        CancelCommand = ReactiveCommand.Create(Cancel, this.WhenAnyValue(x => x.Progress.IsRunning));

        _ = RefreshMediaAsync();
    }

    public ProgressViewModel Progress { get; }
    public ObservableCollection<MediaItemViewModel> SourceMediaItems { get => _sourceMediaItems; set => this.RaiseAndSetIfChanged(ref _sourceMediaItems, value); }
    public ObservableCollection<MediaItemViewModel> DestMediaItems { get => _destMediaItems; set => this.RaiseAndSetIfChanged(ref _destMediaItems, value); }
    public MediaItemViewModel? SourceMedia { get => _sourceMedia; set => this.RaiseAndSetIfChanged(ref _sourceMedia, value); }
    public MediaItemViewModel? DestMedia { get => _destMedia; set => this.RaiseAndSetIfChanged(ref _destMedia, value); }
    public long SrcStartOffset { get => _srcStartOffset; set => this.RaiseAndSetIfChanged(ref _srcStartOffset, value); }
    public long DestStartOffset { get => _destStartOffset; set => this.RaiseAndSetIfChanged(ref _destStartOffset, value); }
    public long Size { get => _size; set => this.RaiseAndSetIfChanged(ref _size, value); }
    public bool Byteswap { get => _byteswap; set => this.RaiseAndSetIfChanged(ref _byteswap, value); }
    public string ErrorMessage { get => _errorMessage; set => this.RaiseAndSetIfChanged(ref _errorMessage, value); }
    public bool HasError { get => _hasError; set => this.RaiseAndSetIfChanged(ref _hasError, value); }

    public ReactiveCommand<Unit, Unit> RefreshMediaCommand { get; }
    public ReactiveCommand<Unit, Unit> StartTransferCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    private async Task RefreshMediaAsync()
    {
        try
        {
            var medias = await _mediaService.ListMediaAsync();
            var items = medias.Select(m => new MediaItemViewModel
                { Path = m.Path, Name = m.Name, DiskSize = m.DiskSize, IsPhysicalDrive = m.IsPhysicalDrive, MediaInfo = m }).ToList();
            SourceMediaItems = new ObservableCollection<MediaItemViewModel>(items);
            DestMediaItems = new ObservableCollection<MediaItemViewModel>(items);
            if (SourceMedia == null && items.Count > 0) SourceMedia = items[0];
            if (DestMedia == null && items.Count > 1) DestMedia = items[1];
        }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
    }

    private async Task StartTransferAsync()
    {
        HasError = false;
        _cts = new CancellationTokenSource();
        Progress.Reset();
        Progress.IsRunning = true;
        try
        {
            var progress = new Progress<Models.ProgressModel>(p =>
            {
                Progress.Update(p);
                if (p.IsComplete && !p.HasError) Progress.IsRunning = false;
            });
            await _imagingService.TransferAsync(_sourceMedia!.Path, SrcStartOffset,
                _destMedia!.Path, DestStartOffset, Size, Byteswap, progress, _cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HasError = true; ErrorMessage = ex.Message; }
        finally { _cts?.Dispose(); _cts = null; Progress.IsRunning = false; }
    }

    private void Cancel() => _cts?.Cancel();
}
