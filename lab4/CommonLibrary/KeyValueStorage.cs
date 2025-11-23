namespace CommonLibrary;

public interface IKeyValueStorage
{
    public void Set(string key, string value);
    public string? Get(string key);
    
    public Dictionary<string, string?> GetAll();
    
}
