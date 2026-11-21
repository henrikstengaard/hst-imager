namespace Hst.Imager.Core.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using Hst.Core;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.Medias;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Media path resolver resolving paths to test medias in memory, otherwise resolves paths on disk.
/// </summary>
public class TestMediaPathResolver : MediaPathResolver
{
    private readonly IList<TestMedia> testMedias;

    public TestMediaPathResolver(IList<TestMedia> testMedias) : base(NullLogger.Instance)
    {
        this.testMedias = testMedias;
    }

    public override Result<MediaResult> ResolveMedia(string path, bool allowNonExisting = false)
    {
        ArgumentNullException.ThrowIfNull(path);

        var testMedia = testMedias.FirstOrDefault(x => path.StartsWith(x.Path, StringComparison.OrdinalIgnoreCase));
        if (testMedia == null)
        {
            return base.ResolveMedia(path, allowNonExisting);
        }

        var directorySeparatorChar = path.IndexOf("\\", StringComparison.OrdinalIgnoreCase) >= 0 ? "\\" : "/";
        var fileSystemPath = path.StartsWith(testMedia.Path, StringComparison.OrdinalIgnoreCase) && path.Length > testMedia.Path.Length
            ? path.Substring(testMedia.Path.Length + 1)
            : string.Empty;

        return new Result<MediaResult>(new MediaResult
        {
            Exists = true,
            FullPath = path,
            MediaPath = testMedia.Path,
            DirectorySeparatorChar = directorySeparatorChar,
            FileSystemPath = fileSystemPath,
            Modifiers = ModifierEnum.None,
            ByteSwap = false
        });
    }
}
