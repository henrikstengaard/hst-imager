using System.ComponentModel.DataAnnotations;

namespace Hst.Imager.GuiApp.Models;

public class OpenExternalViewModel
{
    [Required]
    public string Url { get; set; }
}