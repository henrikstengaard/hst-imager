namespace Hst.Imager.Core.FileSystems.Fat;

public static class OffsetHelper
{
    public static long Calculate(FatFileSystem fatFileSystem, long cluster) =>
        fatFileSystem.FirstDataSector + ((cluster - 2) * fatFileSystem.BiosParameterBlock.SectorsPerCluster);

    public static long CalculateFat16RootDirectorySectors(FatFileSystem fatFileSystem)
    {
        return (fatFileSystem.BiosParameterBlock.RootEntriesCount * Constants.FatEntrySize +
                fatFileSystem.BiosParameterBlock.BytesPerSector - 1) /
               fatFileSystem.BiosParameterBlock.BytesPerSector;
    }

    public static long CalculateRootDirectorySectors(FatFileSystem fatFileSystem)
    {
        return fatFileSystem.BiosParameterBlock.RootEntriesCount == 0
            ? fatFileSystem.BiosParameterBlock.SectorsPerCluster
            : CalculateFat16RootDirectorySectors(fatFileSystem);
    }
}