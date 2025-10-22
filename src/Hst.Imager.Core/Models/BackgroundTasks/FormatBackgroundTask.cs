using System.Text.Json.Serialization;
using System.Threading;

namespace Hst.Imager.Core.Models.BackgroundTasks
{
    public class FormatBackgroundTask : IBackgroundTask
    {
        public string Title { get; set; }
        public string Path { get; set; }
        public FormatType FormatType { get; set; }
        public string FileSystem { get; set; }
        public string FileSystemPath { get; set; }
        public long Size { get; set; }
        public long MaxPartitionSize { get; set; }
        public bool UseExperimental { get; set; }
        public bool Kickstart31 { get; set; }
        public bool Byteswap { get; set; }

        [JsonIgnore]
        public CancellationToken Token { get; set; }

    }
}