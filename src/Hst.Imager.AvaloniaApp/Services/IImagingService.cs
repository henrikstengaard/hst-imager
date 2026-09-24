using System;
using System.Threading;
using System.Threading.Tasks;
using Hst.Imager.AvaloniaApp.Models;
using Hst.Imager.Core.Models;

namespace Hst.Imager.AvaloniaApp.Services;

public interface IImagingService
{
    Task ReadAsync(string sourcePath, string destinationPath, long startOffset, long size, bool byteswap,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    Task WriteAsync(string sourcePath, string destinationPath, long startOffset, long size, bool byteswap,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    Task CompareAsync(string sourcePath, long sourceStartOffset, string destinationPath, long destinationStartOffset,
        long size, bool byteswap, IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    Task TransferAsync(string sourcePath, long srcStartOffset, string destinationPath, long destStartOffset,
        long size, bool byteswap, IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    Task BlankAsync(string path, long size, bool compatibleSize,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    Task OptimizeAsync(string path, long size, bool byteswap,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken);

    Task FormatAsync(string path, FormatType formatType, string fileSystem, string? fileSystemPath,
        long size, long maxPartitionSize, bool useExperimental, bool kickstart31, bool byteswap,
        IProgress<ProgressModel> progress, CancellationToken cancellationToken);
}
