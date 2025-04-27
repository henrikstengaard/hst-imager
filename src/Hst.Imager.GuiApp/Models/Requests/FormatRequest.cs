using Hst.Imager.Core.Models;
using System.ComponentModel.DataAnnotations;

namespace Hst.Imager.GuiApp.Models.Requests
{
    public class FormatRequest
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public string Path { get; set; }

        [Required]
        public FormatType FormatType { get; set; }
        [Required]

        public string FileSystem { get; set; }
        public string FileSystemPath { get; set; }

        public long Size { get; set; }

        public long MaxPartitionSize { get; set; }

        public bool Byteswap { get; set; }
    }
}
