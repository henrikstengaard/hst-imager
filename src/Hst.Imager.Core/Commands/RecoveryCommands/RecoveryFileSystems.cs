using System;
using System.Linq;

namespace Hst.Imager.Core.Commands.RecoveryCommands;

/// <summary>
/// Amiga file systems supported by file system scan, repair and salvage.
/// </summary>
public static class RecoveryFileSystems
{
    private static readonly string[] SupportedDosTypes =
    [
        "DOS\\0", "DOS\\1", "DOS\\2", "DOS\\3", "DOS\\4", "DOS\\5", "DOS\\6", "DOS\\7",
        "PFS\\3", "PDS\\3", "SFS\\0"
    ];

    /// <summary>
    /// File system with formatted dos type, e.g. "PFS\3", is supported.
    /// </summary>
    public static bool IsSupported(string dosTypeFormatted) =>
        SupportedDosTypes.Contains(dosTypeFormatted ?? string.Empty, StringComparer.Ordinal);
}
