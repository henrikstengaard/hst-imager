namespace Hst.Imager.GuiApp.Models.Requests
{
    using System.ComponentModel.DataAnnotations;

    public class TransferRequest
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public string SourcePath { get; set; }
        
        [Required]
        public string DestinationPath { get; set; }
        public long SrcStartOffset { get; set; }
        public long DestStartOffset { get; set; }
        public long Size { get; set; }

        public bool Byteswap { get; set; }
    }
}