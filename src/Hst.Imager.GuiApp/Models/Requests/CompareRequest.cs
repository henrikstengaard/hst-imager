namespace Hst.Imager.GuiApp.Models.Requests
{
    using System.ComponentModel.DataAnnotations;

    public class CompareRequest
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public string SourcePath { get; set; }
        
        public long SourceStartOffset { get; set; }

        [Required]
        public string DestinationPath { get; set; }

        public long DestinationStartOffset { get; set; }

        public long Size { get; set; }

        public bool Byteswap { get; set; }
    }
}