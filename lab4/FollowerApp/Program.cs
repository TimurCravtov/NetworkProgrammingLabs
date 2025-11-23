using CommonLibrary;
using FollowerApp;

var leaderUrl = Environment.GetEnvironmentVariable("LEADER_URL") ?? "";
var port = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "8080");

var follower = new Follower(new InMemoryKeyValueStorage(), port, leaderUrl);
follower.Run();
