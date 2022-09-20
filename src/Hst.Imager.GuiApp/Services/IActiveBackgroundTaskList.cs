namespace Hst.Imager.GuiApp.Services
{
    public interface IActiveBackgroundTaskList
    {
        void Add(ActiveBackgroundWorkItem activeBackgroundWorkItem);
        void Reset();
        void CancelAll();
    }
}