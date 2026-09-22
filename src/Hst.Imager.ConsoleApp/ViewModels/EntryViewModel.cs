using System;
using System.Collections.Generic;
using Hst.Imager.Core.Models.FileSystems;

namespace Hst.Imager.ConsoleApp.ViewModels;

public class EntryViewModel
{
    public string Name { get; set; }
    public string FormattedName { get; set; }
    
    /// <summary>
    /// raw path for internal use
    /// </summary>
    public string RawPath { get; set; }
    
    public string[] RelativePathComponents { get; set; }
    public string[] FullPathComponents { get; set; }

    public long Size { get; set; }
    public EntryType Type { get; set; }
    public DateTime? Date { get; set; }
    
    public IDictionary<string, string> Properties { get; set; }
    public string Attributes { get; set; }

    public EntryViewModel()
    {
        Properties = new Dictionary<string, string>();
    }
}