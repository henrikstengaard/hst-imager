namespace Hst.Imager.Core.PhysicalDrives
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using Models;

    public static class WmicReader
    {
        private static readonly Regex deviceIdRegex = new Regex("DeviceID=\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static IEnumerable<WmicDiskDrive> ParseWmicDiskDrives(string csv)
        {
            var lineCount = 0;

            var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in csv.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                lineCount++;

                var columns = line.Split(',');

                if (lineCount == 1)
                {
                    for (var i = 0; i < columns.Length; i++)
                    {
                        columnIndex[columns[i]] = i;
                    }
                    continue;
                }

                yield return new WmicDiskDrive
                {
                    MediaType = columns[columnIndex["MediaType"]],
                    Model = columns[columnIndex["Model"]],
                    Name = columns[columnIndex["Name"]],
                    Size = long.TryParse(columns[columnIndex["Size"]], out var size) ? size : null,
                    InterfaceType = columns[columnIndex["InterfaceType"]]
                };
            }
        }

        public static IEnumerable<WmicDiskDriveToDiskPartition> ParseWmicDiskDriveToDiskPartitions(string csv)
        {
            foreach (var line in csv.Split(new []{'\n', '\r'}, StringSplitOptions.RemoveEmptyEntries))
            {
                var matches = deviceIdRegex.Matches(line);
                if (matches.Count != 2)
                {
                    continue;
                }

                yield return new WmicDiskDriveToDiskPartition
                {
                    Antecedent = matches[0].Groups[1].Value,
                    Dependent = matches[1].Groups[1].Value,
                };
            }
        }
        
        public static IEnumerable<WmicLogicalDiskToPartition> ParseWmicLogicalDiskToPartitions(string csv)
        {
            foreach (var line in csv.Split(new []{'\n', '\r'}, StringSplitOptions.RemoveEmptyEntries))
            {
                var matches = deviceIdRegex.Matches(line);
                if (matches.Count != 2)
                {
                    continue;
                }

                yield return new WmicLogicalDiskToPartition
                {
                    Antecedent = matches[0].Groups[1].Value,
                    Dependent = matches[1].Groups[1].Value,
                };
            }
        }
    }
}