using CommonLibrary;
using FollowerApp;

var follower = new Follower(new InMemoryKeyValueStorage(), 5000, "");
follower.Run();
