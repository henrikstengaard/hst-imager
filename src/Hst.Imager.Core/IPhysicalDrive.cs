namespace HstWbInstaller.Imager.Core
{
    using System.IO;

    public interface IPhysicalDrive
    {
        string Path { get; }
        string Type { get; }
        string Model { get; }
        long Size { get; }
        bool Writable { get; }
        //RigidDiskBlock RigidDiskBlock { get; set; }

        Stream Open();
        void SetWritable(bool writable);
    }
}