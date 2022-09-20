namespace Hst.Imager.GuiApp.Models
{
    using Hst.Imager.Core.Commands;

    public class MediaInfoViewModel
    {
        public string Path { get; set; }
        public string Model { get; set; }
        public bool IsPhysicalDrive;
        public long DiskSize { get; set; }
        public DiskInfo DiskInfo { get; set; }
    }
}