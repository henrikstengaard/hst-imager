namespace Hst.Imager.Core.Models.BackgroundTasks
{
    using System.Text.Json.Serialization;
    using System.Threading;

    public class WriteBackgroundTask : IBackgroundTask
    {
        public string Title { get; set; }
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public long Size { get; set; }
        public bool Byteswap { get; set; }

        [JsonIgnore]
        public CancellationToken Token { get; set; }
    }
}