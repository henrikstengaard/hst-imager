using Hst.Imager.Core.FileSystems.Fat.Blocks;

namespace Hst.Imager.Core.FileSystems.Fat;

public record FatFileSystem(BiosParameterBlock BiosParameterBlock, ExtendedBiosParameterBlock ExtendedBiosParameterBlock,
    long TotalSectors, long FatSectors, int RootDirSectors, long FirstRootSector, long FirstDataSector, long DataSectors,
    long ClusterCount, FatType FatType);