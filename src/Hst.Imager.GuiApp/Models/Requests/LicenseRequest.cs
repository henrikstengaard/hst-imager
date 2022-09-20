namespace Hst.Imager.GuiApp.Models.Requests
{
    using System.ComponentModel.DataAnnotations;

    public class LicenseRequest
    {
        [Required]
        public bool LicenseAgreed { get; set; }
    }
}