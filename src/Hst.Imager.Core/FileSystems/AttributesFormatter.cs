using System;
using System.IO;
using System.Linq;
using Hst.Amiga.FileSystems;
using Hst.Imager.Core.Models;

namespace Hst.Imager.Core.FileSystems;

public static class AttributesFormatter
{
    public static string FormatAttributes(Core.Models.FileSystems.Entry entry, AttributesMode attributesMode)
    {
        if (attributesMode == AttributesMode.Auto)
        {
            attributesMode = OperatingSystem.IsWindows() ? AttributesMode.Windows : AttributesMode.Unix;
        }
        
        switch (attributesMode)
        {
            case AttributesMode.Amiga:
                var protectionBits = GetProtectionBits(entry);
                return EntryFormatter.FormatProtectionBits(ProtectionBitsConverter.ToProtectionBits(protectionBits));
            case AttributesMode.Unix:
                var unixFileMode = GetUnixFileMode(entry);
                return FormatUnixFileMode(entry.Type, (UnixFileMode)unixFileMode);
            case AttributesMode.Windows:
                var windowsAttributes = GetWindowsAttributes(entry);
                return FormatWindowsAttributes((FileAttributes)windowsAttributes);
            default:
                return string.Empty;
        }
    }

    private static int GetProtectionBits(Core.Models.FileSystems.Entry entry)
    {
        var propertyValue = entry.Properties.TryGetValue(Constants.EntryPropertyNames.ProtectionBits, out var value)
            ? value
            : "";
        
        if (string.IsNullOrWhiteSpace(propertyValue))
        {
            return 0;
        }

        return int.TryParse(propertyValue, out var protectionBitsValue) ? protectionBitsValue : 0;
    }

    private static uint GetUnixFileMode(Core.Models.FileSystems.Entry entry)
    {
        var propertyValue = entry.Properties.TryGetValue(Constants.EntryPropertyNames.UnixFileMode, out var value)
            ? value
            : "";
        
        var defaultUnixFileMode = (uint)(
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupRead |
            UnixFileMode.GroupWrite |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherWrite |
            UnixFileMode.OtherExecute);
        
        if (string.IsNullOrWhiteSpace(propertyValue))
        {
            return defaultUnixFileMode;
        }

        return uint.TryParse(propertyValue, out var uintValue) ? uintValue : defaultUnixFileMode;
    }
    
    private static uint GetWindowsAttributes(Core.Models.FileSystems.Entry entry)
    {
        var propertyValue = entry.Properties.TryGetValue(Constants.EntryPropertyNames.WindowsAttributes, out var value)
            ? value
            : "";

        var defaultWindowsAttributes = (uint)FileAttributes.Archive;
        
        if (string.IsNullOrWhiteSpace(propertyValue))
        {
            return defaultWindowsAttributes;
        }

        return uint.TryParse(propertyValue, out var uintValue) ? uintValue : defaultWindowsAttributes;
    }

    private static string FormatWindowsAttributes(FileAttributes fileAttributes)
    {
        const string windowsAttributes = "ARHS";

        var orderedAttributes = new[]
        {
            FileAttributes.Archive,
            FileAttributes.ReadOnly,
            FileAttributes.Hidden,
            FileAttributes.System
        };

        return FormatAttributes(windowsAttributes,
            orderedAttributes.Select(x => fileAttributes.HasFlag(x)).ToArray());
    }
    
    private static string FormatUnixFileMode(Core.Models.FileSystems.EntryType entryType, UnixFileMode unixFileMode)
    {
        const string unixAttributes = "rwxrwxrwx";

        var orderedPermissions = new[]
        {
            UnixFileMode.UserRead,
            UnixFileMode.UserWrite,
            UnixFileMode.UserExecute,
            UnixFileMode.GroupRead,
            UnixFileMode.GroupWrite,
            UnixFileMode.GroupExecute,
            UnixFileMode.OtherRead,
            UnixFileMode.OtherWrite,
            UnixFileMode.OtherExecute
        };

        return string.Concat(GetTypeAttribute(entryType), FormatAttributes(unixAttributes,
            orderedPermissions.Select(x => unixFileMode.HasFlag(x)).ToArray()));
    }

    private static char GetTypeAttribute(Core.Models.FileSystems.EntryType entryType)
    {
        return entryType switch
        {
            Models.FileSystems.EntryType.Dir => 'd',
            Models.FileSystems.EntryType.LinkDir => 'l',
            Models.FileSystems.EntryType.File => '-',
            Models.FileSystems.EntryType.LinkFile => 'l',
            _ => '-'
        };
    }
    
    private static string FormatAttributes(string attributes, bool[] presentAttributes)
    {
        var attributesArray = attributes.ToCharArray();
        for (var i = 0; i < presentAttributes.Length; i++)
        {
            if (presentAttributes[i])
            {
                continue;
            }

            attributesArray[i] = '-';
        }

        return new string(attributesArray);
    }
}