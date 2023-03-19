namespace Hst.Imager.Core.Models.BackgroundTasks
{
    using System.Text.Json.Serialization;
    using System.Threading;

    public class ListBackgroundTask : IBackgroundTask
    {
        [JsonIgnore]
        public CancellationToken Token { get; set; }
    }
}