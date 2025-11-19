using System.Net.Http.Json;
using CommonLibrary;
using LeaderApp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

public class Leader
{

    private readonly IKeyValueStorage _storage;
    private readonly HashSet<string> _followersUrl;
    private readonly int _writeQuorum;
    private readonly string _url;
    public Leader(IKeyValueStorage storage, HashSet<string> followersUrl, int writeQuorum, int port = 8080)
    {
        _followersUrl = followersUrl;
        _storage = storage;
        _writeQuorum = writeQuorum;
        _url = $"http://0.0.0.0:{port}/";
    }

    public Task RunAsync()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        app = RegisterLeaderEndpoints(app);
        app = CommonRequestHandler.RegisterGetRequest(app, _storage);
        
        return app.RunAsync(_url);
    }
    
    private async Task<bool> HandlePutRequest(string key, string value)
    {
        var quorumReached = new TaskCompletionSource<bool>();
        
        _ = SendToFollowers(key, value, _writeQuorum, () =>
        {
            quorumReached.TrySetResult(true);
        });
        await quorumReached.Task;
        
        _storage.Set(key, value);
        return true;
        
    }
    
    public void Run(bool useAsync = false)
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        app = RegisterLeaderEndpoints(app);
        app = CommonRequestHandler.RegisterGetRequest(app, _storage);

        var url = "http://0.0.0.0:8080/";

        if (useAsync)
        {
            _ = app.RunAsync(url);
        }
        else
        {
            app.Run(url);
        }
    }


    private async Task SendToFollowers(string key, string value, int writeQuorum, Action onQuorumReached)
    {
        using var client = new HttpClient();

        int successCount = 0;
        var lockObj = new object();

        if (writeQuorum == 0) onQuorumReached();
        
        var tasks = _followersUrl.Select(async url =>
        {
            try
            {
                var response = await client.PostAsJsonAsync($"{url}/replicate/", new PutRequest(key, value));

                if (response.IsSuccessStatusCode)
                {
                    bool quorumHit = false;
                    lock (lockObj)
                    {
                        successCount++;
                        if (successCount == writeQuorum)
                            quorumHit = true;
                    }

                    if (quorumHit)
                        onQuorumReached(); 
                }
            }
            catch
            {
                // literally nothing
            }
        }).ToList();

        await Task.WhenAll(tasks);
    }
    
    public void AddFollower(string url)
    {
        _followersUrl.Add(url);
    }
    
    private WebApplication RegisterLeaderEndpoints(WebApplication app)
    {
        app.MapPost("/put", async (PutRequest body) =>
        {
            await HandlePutRequest(body.Key, body.Value);
            return Results.Ok();
        });

        return app;
    }
    
}