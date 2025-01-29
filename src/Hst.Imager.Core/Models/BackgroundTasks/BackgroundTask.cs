namespace Hst.Imager.Core.Models.BackgroundTasks
{
    public class BackgroundTask
    {
        public string Type { get; set; }
        public string Payload { get; set; }
        public bool CancelAll { get; set; }
    }
}