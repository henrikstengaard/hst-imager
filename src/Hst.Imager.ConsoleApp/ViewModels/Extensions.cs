using System.Linq;
using Hst.Imager.Core.Commands;
using Hst.Imager.Core.FileSystems;

namespace Hst.Imager.ConsoleApp.ViewModels;

public static class Extensions
{
    public static EntriesInfoViewModel ToViewModel(this EntriesInfo entriesInfo)
    {
        return new EntriesInfoViewModel
        {
            Path = entriesInfo.Path,
            Recursive = entriesInfo.Recursive,
            Entries = entriesInfo.Entries?.Select(entry => new EntryViewModel
            {
                Name = entry.Name,
                FormattedName = entry.FormattedName,
                RawPath = entry.RawPath,
                RelativePathComponents = entry.RelativePathComponents,
                FullPathComponents = entry.FullPathComponents,
                Size = entry.Size,
                Type = entry.Type,
                Date = entry.Date,
                Properties = entry.Properties,
                Attributes = AttributesFormatter.FormatAttributes(entry, entriesInfo.AttributesMode)
            }).ToList()
        };
    }
}