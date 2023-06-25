namespace Hst.Imager.Core.Commands;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hst.Core;
using Microsoft.Extensions.Logging;

public class BlockViewCommand : CommandBase
{
    private readonly ILogger<BlockViewCommand> logger;
    private readonly byte[] buffer;
    private readonly ICommandHelper commandHelper;
    private readonly IEnumerable<IPhysicalDrive> physicalDrives;
    private readonly string path;
    private readonly int blockSize;
    private readonly long? start;

    private const int BytesPerLine = 16;

    public BlockViewCommand(ILogger<BlockViewCommand> logger, ICommandHelper commandHelper,
        IEnumerable<IPhysicalDrive> physicalDrives, string path, int blockSize, long? start)
    {
        this.logger = logger;
        this.buffer = new byte[blockSize];
        this.commandHelper = commandHelper;
        this.physicalDrives = physicalDrives;
        this.path = path;
        this.blockSize = blockSize;
        this.start = start;
    }

    public override async Task<Result> Execute(CancellationToken token)
    {
        if (blockSize % 512 != 0)
        {
            return new Result(new Error("Block size must be dividable by 512"));
        }

        if (start.HasValue && start.Value % blockSize != 0)
        {
            return new Result(new Error($"Start offset must be dividable by block size {blockSize}"));
        }

        OnInformationMessage($"Reading block from '{path}'");

        OnDebugMessage($"Opening '{path}' as readable");

        var mediaResult = commandHelper.GetReadableMedia(physicalDrives, path);
        if (mediaResult.IsFaulted)
        {
            return new Result(mediaResult.Error);
        }

        using var media = mediaResult.Value;
        var stream = media.Stream;

        var offset = start ?? 0;

        int bytesRead;
        try
        {
            stream.Seek(offset, SeekOrigin.Begin);
            bytesRead = await stream.ReadAsync(this.buffer, 0, this.buffer.Length, token);
        }
        catch (Exception e)
        {
            return new Result(new Error($"Failed to read: {e}"));
        }

        if (bytesRead == 0)
        {
            return new Result(new Error("Read 0 bytes"));
        }

        OnInformationMessage(string.Concat($"Block bytes at offset {offset} (0x{offset:x}):", Environment.NewLine,
            FormatBlockBytes(offset, this.buffer)));

        return new Result();
    }

    private static string FormatBlockBytes(long offset, byte[] blockBytes)
    {
        var offsetWidth = (offset + blockBytes.Length).ToString("x").Length;

        var text = new StringBuilder(16);
        var output = new StringBuilder(10000);

        var byteCount = 0;
        foreach (var blockByte in blockBytes)
        {
            if (byteCount == 0)
            {
                output.Append($"{offset.ToString($"x{offsetWidth}").ToUpperInvariant()}:");
            }

            output.Append($" {blockByte.ToString("x2").ToUpperInvariant()}");
            text.Append(FormatByte(blockByte));

            byteCount++;
            if (byteCount >= BytesPerLine)
            {
                output.AppendLine($" | {text}");
                byteCount = 0;
                text.Clear();
                offset += BytesPerLine;
            }
        }

        return output.ToString();
    }

    private static char FormatByte(byte value)
    {
        return value >= 33 ? (char)value : '.';
    }
}