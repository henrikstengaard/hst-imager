﻿namespace Hst.Imager.Core.Models.BackgroundTasks
{
    using System.Text.Json.Serialization;
    using System.Threading;

    public class OptimizeBackgroundTask : IBackgroundTask
    {
        public string Title { get; set; }
        public string Path { get; set; }
        [JsonIgnore]
        public CancellationToken Token { get; set; }
    }
}