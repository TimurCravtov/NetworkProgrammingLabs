using CommonLibrary;

var followers = Environment.GetEnvironmentVariable("FOLLOWERS")?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet() ?? new HashSet<string>();
var writeQuorum = int.Parse(Environment.GetEnvironmentVariable("WRITE_QUORUM") ?? "0");
var port = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "8080");
var minDelay = float.Parse(Environment.GetEnvironmentVariable("MIN_DELAY_MS") ?? "0.1");
var maxDelay = float.Parse(Environment.GetEnvironmentVariable("MAX_DELAY_MS") ?? "1.0");

var leader = new Leader(new InMemoryKeyValueStorage(), followers, writeQuorum, port, minDelay, maxDelay);
leader.Run();
