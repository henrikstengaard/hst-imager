namespace Hst.Imager.Core.Models.BackgroundTasks
{
    using System.Text.Json.Serialization;
    using System.Threading;

    public class InfoBackgroundTask : IBackgroundTask
    {
        public string Path { get; set; }

        [JsonIgnore]
        public CancellationToken Token { get; set; }
    }
}