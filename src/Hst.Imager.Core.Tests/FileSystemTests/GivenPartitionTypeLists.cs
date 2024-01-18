using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Hst.Imager.Core.Models;
using Xunit;

namespace Hst.Imager.Core.Tests.FileSystemTests;

public class GivenPartitionTypeLists
{
    [Fact(Skip = "Manually used for creating csv list")]
    public async Task CreateCsvList()
    {
        GuidPartitionTypeRegister.Instance.AddDefault();
        
        var guids1 = await Create1();
        var guids2 = await Create2();
        
        var lines1 = guids1.Split(new []{ '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        var lines2 = guids2.Split(new []{ '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

        var index1 = new Dictionary<string, List<string>>();
        foreach (var line in lines1)
        {
            var columns = line.Split("\t");
            var guid = columns[0];
            if (!index1.ContainsKey(guid))
            {
                index1.Add(columns[0], new List<string>());
                
            }
            index1[columns[0]].Add(line);
        }

        var index2 = new Dictionary<string, List<string>>();
        foreach (var line in lines2)
        {
            var columns = line.Split("\t");
            var guid = columns[0];
            if (!index2.ContainsKey(guid))
            {
                index2.Add(columns[0], new List<string>());
                
            }
            index2[columns[0]].Add(line);
        }

        var diff =index1.Where(x => !index2.ContainsKey(x.Key)).SelectMany(x => x.Value).ToList();
        
        // foreach (var item in index1)
        // {
        //     if (!index2.ContainsKey(item.Key))
        //     {
        //         
        //     }
        // }

        var total = string.Concat(guids2, Environment.NewLine, Environment.NewLine, string.Join(Environment.NewLine, diff));
    }
    
    private async Task<string> Create1()
    {
        var refRegex = new Regex(@"\[(\d+|[a-z]+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var osRegex = new Regex("^\\+(.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        var codeLines = new List<string>();
        var lines = await File.ReadAllLinesAsync(@"FileSystemTests/partition_guids.txt");

        var os = string.Empty;
        
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var osMatch = osRegex.Match(line);

            if (osMatch.Success)
            {
                os = refRegex.Replace(osMatch.Groups[1].Value, string.Empty).Trim();
                continue;
            }

            //var tline = refRegex.Replace(line, string.Empty);
            var columns = line.Split('\t');

            if (columns.Length != 2)
            {
                continue;
            }

            var partitionType = refRegex.Replace(columns[0], string.Empty).Trim();
            var guidType = refRegex.Replace(columns[1], string.Empty).Trim().ToLower();
            codeLines.Add(string.Join("\t",new[]{ guidType.ToLower(), $"{os} {partitionType}"}));
            //"$"AddType(\"{os}\", \"{partitionType}\", \"{guidType}\");"));

        }
        
        return string.Join(Environment.NewLine, codeLines);
    }
    
    private async Task<string> Create2()
    {
        //var refRegex = new Regex(@"\[(\d+|[a-z]+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var addTypeRegex = new Regex("AddType\\([^,]+,\\s+\"([^\"]+)\",\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        
        var codeLines = new List<string>();
        var lines = await File.ReadAllLinesAsync(@"FileSystemTests/partition_guids2.txt");
        
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var addTypeMatch = addTypeRegex.Match(line);

            if (!addTypeMatch.Success)
            {
                continue;
            }

            var guidType  = addTypeMatch.Groups[1].Value.Trim().ToLower();
            var name  = addTypeMatch.Groups[2].Value.Trim();

            codeLines.Add(string.Join("\t", new[]{ guidType, name }));
        }
        
        return string.Join(Environment.NewLine, codeLines);
    }
}