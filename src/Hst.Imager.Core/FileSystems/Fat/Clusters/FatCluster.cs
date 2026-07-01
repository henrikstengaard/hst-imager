using System;
using System.Collections.Generic;

namespace Hst.Imager.Core.FileSystems.Fat.Clusters;

public static class FatCluster
{
    public static IEnumerable<uint> ReadClusterChain(FatType fatType, byte[] fatBytes, uint firstCluster) =>
        fatType switch
        {
            FatType.Fat12 => Fat12Cluster.ReadClusterChain(fatBytes, (ushort)firstCluster),
            FatType.Fat16 => Fat16Cluster.ReadClusterChain(fatBytes, (ushort)firstCluster),
            FatType.Fat32 => Fat32Cluster.ReadClusterChain(fatBytes, firstCluster),
            FatType.None => throw new NotSupportedException($"Unsupported FAT type {fatType}"),
            _ => throw new NotSupportedException($"Unsupported FAT type {fatType}")
        };
}