namespace Hst.Imager.Core.PhysicalDrives
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Claunia.PropertyList;
    using Models;

    public static class DiskUtilReader
    {
        public static IEnumerable<DiskUtilDisk> ParseList(Stream stream)
        {
            var pList = PropertyListParser.Parse(stream) as NSDictionary;

            if (pList == null)
            {
                throw new IOException("Invalid diskutil info plist");
            }
            
            var allDisksAndPartitions = pList.ObjectForKey("AllDisksAndPartitions") as NSArray;

            if (allDisksAndPartitions == null)
            {
                throw new IOException("Invalid AllDisksAndPartitions key");
            }

            return ParseDisks(allDisksAndPartitions);
        }

        private static IEnumerable<DiskUtilDisk> ParseDisks(NSArray allDisksAndPartitions)
        {
            foreach (var item in allDisksAndPartitions)
            {
                var dict = item as NSDictionary;
                
                if (dict == null)
                {
                    throw new IOException("Invalid AllDisksAndPartitions item");
                }

                yield return ParseDisk(dict);
            }
        }

        private static DiskUtilDisk ParseDisk(NSDictionary allDisksAndPartitionsDictionary)
        {
            return new DiskUtilDisk
            {
                Content = GetString(allDisksAndPartitionsDictionary, "Content"),
                DeviceIdentifier = GetString(allDisksAndPartitionsDictionary, "DeviceIdentifier"),
                Size = GetLongNumber(allDisksAndPartitionsDictionary, "Size"),
                Partitions = ParsePartitions(allDisksAndPartitionsDictionary),
                ApfsPhysicalStores = ParseApfsPhysicalStores(allDisksAndPartitionsDictionary)
            };
        }

        private static ApfsPhysicalStores ParseApfsPhysicalStores(NSDictionary allDisksAndPartitionsDictionary)
        {
            if (!allDisksAndPartitionsDictionary.ContainsKey("APFSPhysicalStores"))
            {
                return null;
            }

            var apfsPhysicalStores = allDisksAndPartitionsDictionary.ObjectForKey("APFSPhysicalStores") as NSArray;

            var deviceIdentifier = string.Empty;
            foreach (var item in apfsPhysicalStores)
            {
                var dict = item as NSDictionary;

                if (dict == null)
                {
                    throw new IOException("Invalid APFSPhysicalStores item");
                }

                if (!dict.ContainsKey("DeviceIdentifier"))
                {
                    continue;
                }

                deviceIdentifier = GetString(dict, "DeviceIdentifier");
            }

            if (string.IsNullOrWhiteSpace(deviceIdentifier))
            {
                return null;
            }

            return new ApfsPhysicalStores
            {
                DeviceIdentifier = deviceIdentifier
            };
        }

        private static IEnumerable<DiskUtilPartition> ParsePartitions(NSDictionary allDisksAndPartitionsDictionary)
        {
            var partitions = allDisksAndPartitionsDictionary.ObjectForKey("Partitions") as NSArray;

            if (partitions == null)
            {
                yield break;
            }
            
            foreach (var item in partitions)
            {
                var dict = item as NSDictionary;

                if (dict == null)
                {
                    throw new IOException("Invalid Partitions item");
                }

                yield return ParsePartition(dict);
            }
        }
        
        private static DiskUtilPartition ParsePartition(NSDictionary dict)
        {
            return new DiskUtilPartition
            {
                DeviceIdentifier = GetString(dict, "DeviceIdentifier"),
                Size = GetLongNumber(dict, "Size")
            };
        }

        public static DiskUtilInfo ParseInfo(Stream stream)
        {
            var pList = PropertyListParser.Parse(stream) as NSDictionary;

            if (pList == null)
            {
                throw new IOException("Invalid diskutil info plist");
            }
            
            var deviceBlockSize = GetLongNumber(pList, "DeviceBlockSize");
            var busProtocol = GetString(pList, "BusProtocol");
            var ioRegistryEntryName = GetString(pList, "IORegistryEntryName");
            var size = GetLongNumber(pList, "Size");
            var parentWholeDisk = pList.ContainsKey("ParentWholeDisk")
                ? GetString(pList, "ParentWholeDisk")
                : string.Empty;
            var deviceNode = GetString(pList, "DeviceNode");
            var mediaType = GetString(pList, "MediaType");

            var virtualOrPhysical = pList.ContainsKey("VirtualOrPhysical")
                ? GetString(pList, "VirtualOrPhysical")
                : mediaType;

            return new DiskUtilInfo
            {
                DeviceBlockSize = deviceBlockSize,
                BusProtocol = busProtocol,
                IoRegistryEntryName = ioRegistryEntryName,
                Size = size,
                DeviceNode = deviceNode,
                ParentWholeDisk = parentWholeDisk,
                MediaType = mediaType,
                DiskType = GetDiskType(virtualOrPhysical)
            };
        }

        private static DiskUtilInfo.DiskTypeEnum GetDiskType(string virtualOrPhysical)
        {
            return virtualOrPhysical.ToLower() switch
            {
                "physical" => DiskUtilInfo.DiskTypeEnum.Physical,
                "virtual" => DiskUtilInfo.DiskTypeEnum.Virtual,
                "generic" => DiskUtilInfo.DiskTypeEnum.Generic,
                _ => DiskUtilInfo.DiskTypeEnum.Unknown
            };
        }

        private static string GetString(NSDictionary dict, string key)
        {
            var stringObject = dict.ObjectForKey(key) as NSString;

            if (stringObject == null)
            {
                throw new IOException($"Invalid {key} key");
            }

            return stringObject.Content;
        }
        
        private static long GetLongNumber(NSDictionary dict, string key)
        {
            var nsNumber = dict.ObjectForKey(key) as NSNumber;

            if (nsNumber == null)
            {
                return -1;
            }
            
            switch (nsNumber.GetNSNumberType())
            {
                case NSNumber.BOOLEAN:
                    return nsNumber.ToBool() ? 1 : 0;
                case NSNumber.INTEGER:
                    return nsNumber.ToLong();
                case NSNumber.REAL:
                    return Convert.ToInt64(nsNumber.ToDouble());
                default:
                    return -1;
            }
        }
    }
}