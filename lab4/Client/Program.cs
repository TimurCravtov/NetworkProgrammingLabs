using CommonLibrary;
using FollowerApp;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var followerTask = new Follower(
    new InMemoryKeyValueStorage(), 8001, "http://localhost:8000/"
).RunAsync();

var leaderTask = new Leader(
    new InMemoryKeyValueStorage(),
    new HashSet<string> { "http://localhost:8001" },
    1, 8080
).RunAsync();

await Task.WhenAll(followerTask, leaderTask);