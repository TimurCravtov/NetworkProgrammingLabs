using System.Collections.Concurrent;

namespace CommonLibrary;

public class InMemoryKeyValueStorage : IKeyValueStorage
{
    private readonly ConcurrentDictionary<string, string> _storage = new();

    public string? Get(string key)
    {
        return _storage.GetValueOrDefault(key); 
    }

    public void Set(string key, string value)
    {
        _storage[key] = value;
    }

    public override int GetHashCode()
    {
        return _storage.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        return _storage.Equals(obj);
    }
}