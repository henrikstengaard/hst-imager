namespace Hst.Imager.GuiApp.Services
{
    using System.Threading;

    public class ActiveBackgroundWorkItem
    {
        public CancellationTokenSource TokenSource { get; set; }
    }
}