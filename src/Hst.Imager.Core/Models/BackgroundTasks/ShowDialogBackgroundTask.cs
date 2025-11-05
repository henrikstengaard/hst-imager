namespace Hst.Imager.Core.Models.BackgroundTasks
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Serialization;
    using System.Threading;

    public class ShowDialogBackgroundTask : IBackgroundTask
    {
        [JsonIgnore]
        public CancellationToken Token { get; set; }
        public string Id { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public IEnumerable<FileFilter> FileFilters { get; set; }
        public bool PromptCreate { get; set; }

        public ShowDialogBackgroundTask()
        {
            FileFilters = Enumerable.Empty<FileFilter>();
        }
    }
}