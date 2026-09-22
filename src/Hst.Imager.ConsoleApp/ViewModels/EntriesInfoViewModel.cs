using System.Collections.Generic;

namespace Hst.Imager.ConsoleApp.ViewModels;

public class EntriesInfoViewModel
{
    public string Path { get; set; }
    public IEnumerable<EntryViewModel> Entries { get; set; }
    public bool Recursive { get; set; }
}