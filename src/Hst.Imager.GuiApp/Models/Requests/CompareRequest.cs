namespace Hst.Imager.GuiApp.Models.Requests
{
    using System.ComponentModel.DataAnnotations;

    public class CompareRequest
    {
        public enum SourceTypeEnum
        {
            ImageFile,
            PhysicalDisk
        }
        
        [Required]
        public string Title { get; set; }

        [Required] 
        public SourceTypeEnum SourceType { get; set; }
        
        [Required]
        public string SourcePath { get; set; }

        [Required]
        public string DestinationPath { get; set; }
        
        public long Size { get; set; }
        public bool Force { get; set; }
        public int Retries { get; set; }
        public bool Byteswap { get; set; }

        public CompareRequest()
        {
            Force = false;
            Retries = 5;
        }
    }
}