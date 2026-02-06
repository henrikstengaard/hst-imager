using System;
using System.Collections.Generic;
using Hst.Imager.Core.Caching;

namespace Hst.Imager.Core.Tests;

/// <summary>
/// Test implementation of IAppCache for unit testing.
/// Stores cache entries in memory and keeps track of add and get operations.
/// </summary>
public class TestAppCache : IAppCache
{
    public readonly IDictionary<string, object> Cache = new Dictionary<string, object>();

    public readonly List<string> AddHistory = [];
    public readonly List<string> GetHistory = [];
        
    public void Dispose()
    {
    }

    public void Add(string key, object value)
    {
        AddHistory.Add(key);
        Cache[key] = value;
    }

    public void Add(string key, object value, DateTimeOffset absoluteExpiration)
    {
        AddHistory.Add(key);
        Cache[key] = value;
    }

    public object Get(string key)
    {
        GetHistory.Add(key);
        return Cache.TryGetValue(key, out var value) ? value : null;
    }

    public bool Contains(string key)
    {
        GetHistory.Add(key);
        return Cache.ContainsKey(key);
    }
}