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
        public AssetAction AssetAction { get; set; }
        public string AssetPath { get; set; }
        public long Size { get; set; }
        public bool Byteswap { get; set; }

        [JsonIgnore]
        public CancellationToken Token { get; set; }

    }
}