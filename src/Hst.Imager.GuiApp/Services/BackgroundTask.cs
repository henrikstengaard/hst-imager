namespace Hst.Imager.GuiApp.Services
{
    using System.Threading;
    using Hst.Imager.Core.Models.BackgroundTasks;

    public class BackgroundTask : IBackgroundTask
    {
        public CancellationToken Token { get; set; }
    }
}