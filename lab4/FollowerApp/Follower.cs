using System.Net;
using CommonLibrary;
using LeaderApp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FollowerApp;

public class Follower
{
    private readonly IKeyValueStorage _storage;
    private readonly int _port;
    private readonly string _url;
    private readonly string _leaderUrl;

    public Follower(IKeyValueStorage storage, int port, string leaderUrl)
    {
        _port = port;
        _storage = storage;
        _url = $"http://0.0.0.0:{port}/";
        _leaderUrl = leaderUrl;
    }

    private WebApplication RegisterReplicationRoute(WebApplication app)
    {
        app.MapPost("replicate", (PutRequest request, HttpContext ctx) =>
        {
            var leaderUri = new Uri(_leaderUrl);
            var leaderIps = Dns.GetHostAddresses(leaderUri.Host);
            var remoteIp = ctx.Connection.RemoteIpAddress;

            bool IsSameIp(IPAddress ip1, IPAddress ip2)
            {
                if (ip1.Equals(ip2)) return true;
                if (ip1.IsIPv4MappedToIPv6 && ip1.MapToIPv4().Equals(ip2)) return true;
                if (ip2.IsIPv4MappedToIPv6 && ip2.MapToIPv4().Equals(ip1)) return true;
                return false;
            }

            bool authorized = leaderIps.Any(ip => IsSameIp(ip, remoteIp));

            if (!authorized)
            {
                return Results.Unauthorized();
            }

            _storage.Set(request.Key, request.Value);
            Log.Info($"Value set successfully. Key: {request.Key}, Value: {request.Value}");

            return Results.Ok();
        });

        return app;
    }


    public void Run()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();
        app = CommonRequestHandler.RegisterGetAllRequest(app, _storage);
        app = CommonRequestHandler.RegisterGetRequest(app, _storage);
        app = RegisterReplicationRoute(app);

        Log.Info($"Follower running on {_url}");

        app.Run(_url);
    }
}
