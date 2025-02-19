using Hst.Imager.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace Hst.Imager.GuiApp.Models.Requests
{
    public class FormatRequest
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
        public string Path { get; set; }

        [Required]
        public FormatType FormatType { get; set; }
        [Required]

        public string FileSystem { get; set; }
        public string FileSystemPath { get; set; }

        public long Size { get; set; }

        public bool Byteswap { get; set; }
    }
}
