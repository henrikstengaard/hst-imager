namespace Hst.Imager.GuiApp.Services
{
    using System;
    using System.Threading.Tasks;
    using Hst.Imager.Core.Models.BackgroundTasks;

    public class QueuedBackgroundTask
    {
        public readonly Func<IBackgroundTaskContext, ValueTask> WorkItem;
        public readonly IBackgroundTask BackgroundTask;

        public QueuedBackgroundTask(Func<IBackgroundTaskContext, ValueTask> workItem, IBackgroundTask backgroundTask)
        {
            WorkItem = workItem;
            BackgroundTask = backgroundTask;
        }
    }
}