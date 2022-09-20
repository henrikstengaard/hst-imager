namespace Hst.Imager.GuiApp.BackgroundTasks
{
    using System.Threading.Tasks;
    using Hst.Imager.Core.Models.BackgroundTasks;

    public interface IBackgroundTaskHandler
    {
        ValueTask Handle(IBackgroundTaskContext backgroundTaskContext);
    }
}