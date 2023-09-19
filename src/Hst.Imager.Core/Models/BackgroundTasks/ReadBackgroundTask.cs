namespace Hst.Imager.Core.Models.BackgroundTasks
{
    using System.Text.Json.Serialization;
    using System.Threading;

    public class ReadBackgroundTask : IBackgroundTask
    {
        public string Title { get; set; }
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public long Size { get; set; }
        public bool Verify { get; set; }
        public bool Force { get; set; }
        public int Retries { get; set; }
        public bool Byteswap { get; set; }

        [JsonIgnore]
        public CancellationToken Token { get; set; }
        
        public ReadBackgroundTask()
        {
            Verify = false;
            Force = false;
            Retries = 5;
        }
    }
}