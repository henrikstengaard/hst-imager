using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Hst.Imager.Core.Models;

public class GuidPartitionTypeRegister
{
    private readonly Dictionary<Guid, GuidPartitionType> index;

    public GuidPartitionTypeRegister()
    {
        index = new Dictionary<Guid, GuidPartitionType>();
    }

    private static readonly Lazy<GuidPartitionTypeRegister> LazyInstance =
        new(() => new GuidPartitionTypeRegister(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static GuidPartitionTypeRegister Instance => LazyInstance.Value;

    public void AddDefault()
    {
        var assembly = GetType().Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(x => x.EndsWith("guid-partition-types.csv"));
        
        if (string.IsNullOrEmpty(resourceName))
        {
            return;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return;
        }
        using var reader = new StreamReader(stream);
        var guidPartitionTypes = reader.ReadToEnd();

        var lines = guidPartitionTypes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var columns = line.Split('\t');
            if (columns.Length != 2)
            {
                continue;
            }
            
            AddType(columns[0], columns[1]);
        }
    }
    
    public void AddType(string guidType, string partitionType)
    {
        var guidPartitionType = new GuidPartitionType
        {
            GuidType = new Guid(guidType),
            PartitionType = partitionType
        };
        index[guidPartitionType.GuidType] = guidPartitionType;
    }

    public bool TryGet(Guid guidType, out GuidPartitionType guidPartitionType)
    {
        guidPartitionType = null;
        if (!index.ContainsKey(guidType))
        {
            return false;
        }

        guidPartitionType = index[guidType];
        return true;
    }
}