using System;
using System.Runtime.Caching;

namespace Hst.Imager.Core.Caching;

public class MemoryAppCache : IAppCache
{
    private readonly MemoryCache cache;
    private readonly TimeSpan expiresIn;

    public MemoryAppCache()
        : this(Guid.NewGuid().ToString(), TimeSpan.FromMinutes(10))
    {
    }

    public MemoryAppCache(string name, TimeSpan expiresIn)
    {
        cache = new MemoryCache(name);
        this.expiresIn = expiresIn;
    }
    
    public void Add(string key, object value)
    {
        cache.Add(key, value, DateTimeOffset.Now.Add(expiresIn));
    }

    public void Add(string key, object value, DateTimeOffset absoluteExpiration)
    {
        cache.Add(key, value, absoluteExpiration);
    }

    public object Get(string key)
    {
        return cache.Get(key);
    }

    public bool Contains(string key)
    {
        return cache.Contains(key);
    }

    public void Dispose()
    {
        cache.Dispose();
    }
}