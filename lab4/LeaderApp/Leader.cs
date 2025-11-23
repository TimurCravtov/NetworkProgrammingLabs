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
    private readonly (float, float) delayMs;
    private readonly HttpClient _httpClient;
    
    public Leader(IKeyValueStorage storage, HashSet<string> followersUrl, int writeQuorum, int port = 8080, float minDelayMs = 0.1f, float maxDelayMs = 1f)
    {
        _followersUrl = followersUrl;
        _storage = storage;
        _writeQuorum = writeQuorum;
        _url = $"http://0.0.0.0:{port}/";
        delayMs.Item1 = minDelayMs;
        delayMs.Item2 = maxDelayMs;
        _httpClient = new HttpClient();
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
        },
            onQuorumImpossibleToReach: () => quorumReached.TrySetResult(false), delayMs);
        
        bool reached = await quorumReached.Task;

        if (reached)
        {
            _storage.Set(key, value);
        }
        
        return reached;
        
    }
    
    public void Run(bool useAsync = false)
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        app = RegisterLeaderEndpoints(app);
        app = CommonRequestHandler.RegisterGetRequest(app, _storage);
        app = CommonRequestHandler.RegisterGetAllRequest(app, _storage);
        
        if (useAsync)
        {
            _ = app.RunAsync(_url);
        }
        else
        {
            app.Run(_url);
        }
    }


    private async Task SendToFollowers(string key, string value, int writeQuorum, Action onQuorumReached, Action onQuorumImpossibleToReach, (float, float) delayMs = default)
    {
        int successCount = 0;
        var lockObj = new object();

        if (writeQuorum == 0) onQuorumReached();

        (int, int) delayMicroseconds = ((int) Math.Round(delayMs.Item1 * 1000), (int) Math.Round(delayMs.Item2 * 1000));
            
        var tasks = _followersUrl.Select(async url =>
        {
            try
            {
                int randomDelayMicros = Random.Shared.Next(delayMicroseconds.Item1, delayMicroseconds.Item2 + 1000); 
                
                await Task.Delay(TimeSpan.FromMicroseconds(randomDelayMicros));
                
                var response = await _httpClient.PostAsJsonAsync($"{url}/replicate/", new PutRequest(key, value));

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
        if (successCount < writeQuorum) onQuorumImpossibleToReach();
    }
    
    public void AddFollower(string url)
    {
        _followersUrl.Add(url);
    }
    
    private WebApplication RegisterLeaderEndpoints(WebApplication app)
    {
        app.MapPost("/put", async (PutRequest body) =>
        {
            Log.Info("Received put");
            var success = await HandlePutRequest(body.Key, body.Value);
            return success ? Results.Ok() : Results.BadRequest();
        });

        return app;
    }
    
}