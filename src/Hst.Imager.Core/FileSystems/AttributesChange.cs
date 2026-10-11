using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hst.Amiga.FileSystems;
using Hst.Core;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.FileSystems;

/// <summary>
/// Change of attributes parsed from attribute tokens like "+s", "-wd", "rwed" or "755".
/// Tokens starting with '+' add attributes, tokens starting with '-' remove attributes and
/// tokens without '+' or '-' set attributes exactly (other attributes are cleared).
/// Amiga mode uses protection bits "hsparwed", Windows mode uses attributes "rash" and
/// Unix mode uses permissions "rwx" (applied to user, group and other) or octal file mode.
/// </summary>
public class AttributesChange
{
    private const int WindowsAttributesMask = (int)(FileAttributes.ReadOnly | FileAttributes.Hidden |
                                                    FileAttributes.System | FileAttributes.Archive);

    private const int UnixPermissionsMask = 0x1ff; // octal 777

    private const int UnixFileModeMask = 0xfff; // octal 7777

    private static readonly IDictionary<char, int> AmigaFlags = new Dictionary<char, int>
    {
        { 'h', (int)ProtectionBits.HeldResident },
        { 's', (int)ProtectionBits.Script },
        { 'p', (int)ProtectionBits.Pure },
        { 'a', (int)ProtectionBits.Archive },
        { 'r', (int)ProtectionBits.Read },
        { 'w', (int)ProtectionBits.Write },
        { 'e', (int)ProtectionBits.Executable },
        { 'd', (int)ProtectionBits.Delete }
    };

    private static readonly IDictionary<char, int> WindowsFlags = new Dictionary<char, int>
    {
        { 'r', (int)FileAttributes.ReadOnly },
        { 'a', (int)FileAttributes.Archive },
        { 's', (int)FileAttributes.System },
        { 'h', (int)FileAttributes.Hidden }
    };

    private static readonly IDictionary<char, int> UnixFlags = new Dictionary<char, int>
    {
        { 'r', (int)(UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead) },
        { 'w', (int)(UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite) },
        { 'x', (int)(UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute) }
    };

    /// <summary>
    /// Attributes mode the change applies to. Amiga values are protection bits with "rwed" set when allowed,
    /// Windows values are file attributes and Unix values are file modes.
    /// </summary>
    public AttributesMode Mode { get; }

    /// <summary>
    /// Attributes to set exactly, if any. Attributes within mask not present are cleared.
    /// </summary>
    public int? ExactAttributes { get; }

    /// <summary>
    /// Mask of attributes affected by exact attributes.
    /// </summary>
    public int ExactMask { get; }

    /// <summary>
    /// Attributes to add.
    /// </summary>
    public int AddAttributes { get; }

    /// <summary>
    /// Attributes to remove.
    /// </summary>
    public int RemoveAttributes { get; }

    private AttributesChange(AttributesMode mode, int? exactAttributes, int exactMask, int addAttributes,
        int removeAttributes)
    {
        Mode = mode;
        ExactAttributes = exactAttributes;
        ExactMask = exactMask;
        AddAttributes = addAttributes;
        RemoveAttributes = removeAttributes;
    }

    /// <summary>
    /// Apply change to current attributes.
    /// </summary>
    /// <param name="attributes">Current attributes.</param>
    /// <returns>Changed attributes.</returns>
    public int Apply(int attributes)
    {
        if (ExactAttributes.HasValue)
        {
            attributes = (attributes & ~ExactMask) | ExactAttributes.Value;
        }

        attributes |= AddAttributes;
        attributes &= ~RemoveAttributes;

        // normal attribute is only valid, when no other attributes are set
        if (Mode == AttributesMode.Windows && (attributes & ~(int)FileAttributes.Normal) != 0)
        {
            attributes &= ~(int)FileAttributes.Normal;
        }

        return attributes;
    }

    /// <summary>
    /// Format attributes for mode.
    /// </summary>
    /// <param name="attributes">Attributes to format.</param>
    /// <returns>Formatted attributes.</returns>
    public string Format(int attributes)
    {
        return Mode switch
        {
            AttributesMode.Amiga => EntryFormatter.FormatProtectionBits((ProtectionBits)attributes),
            AttributesMode.Windows => AttributesFormatter.FormatWindowsAttributes((FileAttributes)attributes),
            AttributesMode.Unix => AttributesFormatter.FormatUnixFileMode(Models.FileSystems.EntryType.File,
                (UnixFileMode)attributes)[1..],
            _ => attributes.ToString()
        };
    }

    /// <summary>
    /// Resolve attributes mode to use. Auto uses entry iterator attributes mode or attributes mode of
    /// operating system, if entry iterator attributes mode is also auto.
    /// </summary>
    /// <param name="attributesMode">Attributes mode requested.</param>
    /// <param name="entryIteratorAttributesMode">Attributes mode of entry iterator.</param>
    /// <returns>Resolved attributes mode.</returns>
    public static AttributesMode ResolveMode(AttributesMode attributesMode, AttributesMode entryIteratorAttributesMode)
    {
        if (attributesMode != AttributesMode.Auto)
        {
            return attributesMode;
        }

        if (entryIteratorAttributesMode != AttributesMode.Auto)
        {
            return entryIteratorAttributesMode;
        }

        return System.OperatingSystem.IsWindows() ? AttributesMode.Windows : AttributesMode.Unix;
    }

    /// <summary>
    /// Parse attribute tokens.
    /// </summary>
    /// <param name="mode">Attributes mode to parse attribute tokens for.</param>
    /// <param name="tokens">Attribute tokens, e.g. "+s", "-wd", "rwed" or "755".</param>
    /// <returns>Attributes change parsed.</returns>
    public static Result<AttributesChange> Parse(AttributesMode mode, IEnumerable<string> tokens)
    {
        var flags = mode switch
        {
            AttributesMode.Amiga => AmigaFlags,
            AttributesMode.Windows => WindowsFlags,
            AttributesMode.Unix => UnixFlags,
            _ => null
        };

        if (flags == null)
        {
            return new Result<AttributesChange>(new Error($"Unsupported attributes mode '{mode}'"));
        }

        var validFlags = string.Concat(flags.Keys);
        var tokensList = (tokens ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        if (tokensList.Count == 0)
        {
            return new Result<AttributesChange>(new Error($"No attributes specified, valid attributes are '{validFlags}'"));
        }

        int? exactAttributes = null;
        var exactMask = 0;
        var addAttributes = 0;
        var removeAttributes = 0;

        foreach (var token in tokensList)
        {
            // octal file mode
            if (mode == AttributesMode.Unix && token.Length <= 4 && token.All(x => x is >= '0' and <= '7'))
            {
                exactAttributes = (exactAttributes ?? 0) | Convert.ToInt32(token, 8);
                exactMask = UnixFileModeMask;
                continue;
            }

            char? sign = null;
            foreach (var chr in token)
            {
                if (chr is '+' or '-')
                {
                    sign = chr;
                    continue;
                }

                if (!flags.TryGetValue(char.ToLowerInvariant(chr), out var flag))
                {
                    return new Result<AttributesChange>(new Error(
                        $"Invalid attribute '{chr}' in '{token}', valid {mode.ToString().ToLowerInvariant()} attributes are '{validFlags}'"));
                }

                switch (sign)
                {
                    case '+':
                        addAttributes |= flag;
                        removeAttributes &= ~flag;
                        break;
                    case '-':
                        removeAttributes |= flag;
                        addAttributes &= ~flag;
                        break;
                    default:
                        exactAttributes = (exactAttributes ?? 0) | flag;
                        exactMask |= GetExactMask(mode);
                        break;
                }
            }
        }

        return new Result<AttributesChange>(new AttributesChange(mode, exactAttributes, exactMask, addAttributes,
            removeAttributes));
    }

    private static int GetExactMask(AttributesMode mode) => mode switch
    {
        AttributesMode.Amiga => 0xff,
        AttributesMode.Windows => WindowsAttributesMask,
        AttributesMode.Unix => UnixPermissionsMask,
        _ => 0
    };
}
