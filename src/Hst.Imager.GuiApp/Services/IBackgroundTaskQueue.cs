namespace Hst.Imager.GuiApp.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Hst.Imager.Core.Models.BackgroundTasks;

    public interface IBackgroundTaskQueue
    {
        ValueTask QueueBackgroundWorkItemAsync(Func<IBackgroundTaskContext, ValueTask> workItem, IBackgroundTask backgroundTask = null);

        ValueTask<QueuedBackgroundTask> DequeueAsync(
            CancellationToken cancellationToken);
    }
}