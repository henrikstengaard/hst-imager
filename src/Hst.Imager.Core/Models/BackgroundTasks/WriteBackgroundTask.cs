using System.ComponentModel.DataAnnotations;

namespace Hst.Imager.Core.Models.BackgroundTasks
{
    using System.Text.Json.Serialization;
    using System.Threading;

    public class WriteBackgroundTask : IBackgroundTask
    {
        public string Title { get; set; }
        public bool WritePhysicalDisk { get; set; }
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public long StartOffset { get; set; }
        public long Size { get; set; }
        public bool Byteswap { get; set; }

        [JsonIgnore]
        public CancellationToken Token { get; set; }
    }
}