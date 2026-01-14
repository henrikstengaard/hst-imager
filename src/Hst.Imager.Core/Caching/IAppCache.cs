using System;

namespace Hst.Imager.Core.Caching;

public interface IAppCache : IDisposable
{
    void Add(string key, object value);
    void Add(string key, object value, DateTimeOffset absoluteExpiration);
    object Get(string key);
    bool Contains(string key);
}