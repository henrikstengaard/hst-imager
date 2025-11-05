namespace Hst.Imager.GuiApp.Models.Requests
{
    using System.ComponentModel.DataAnnotations;

    public class InfoRequest
    {
        [Required] 
        public string Path { get; set; }

        public bool Byteswap { get; set; }
        
        public bool AllowNonExisting { get; set; }
    }
}