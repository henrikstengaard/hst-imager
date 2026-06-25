using Hst.Imager.Core.FileSystems.Fat.Blocks;

namespace Hst.Imager.Core.FileSystems.Fat;

public record FatFileSystem(BiosParameterBlock BiosParameterBlock, ExtendedBiosParameterBlock ExtendedBiosParameterBlock,
    long TotalSectors, long FatSectors, long FirstRootSector, long RootSectors, long FirstDataSector, long DataSectors,
    long ClusterCount, FatType FatType);