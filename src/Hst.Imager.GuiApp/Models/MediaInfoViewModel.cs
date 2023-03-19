namespace Hst.Imager.GuiApp.Models
{
    public class MediaInfoViewModel
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public bool IsPhysicalDrive;
        public long DiskSize { get; set; }
        public DiskInfoViewModel DiskInfo { get; set; }
    }
}