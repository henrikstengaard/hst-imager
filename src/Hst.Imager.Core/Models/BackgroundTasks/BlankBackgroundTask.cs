namespace Hst.Imager.Core.Models.BackgroundTasks
{
    using System.Text.Json.Serialization;
    using System.Threading;

    public class BlankBackgroundTask : IBackgroundTask
    {
        public string Title { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public bool CompatibleSize { get; set; }
        [JsonIgnore]
        public CancellationToken Token { get; set; }
    }
}