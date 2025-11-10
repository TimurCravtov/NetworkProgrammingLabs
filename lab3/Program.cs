using MemoryScramble.Boards;
using MemoryScramble.Commands;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

public static class Program
{
    public static async Task<WebApplication> StartServerAsync(string[] args)
    {
        var serverPort = args.Length > 0 ? int.Parse(args[0]) : 8080;
        var boardFile = args.Length > 1 ? args[1] : "zoom.txt";

        // await Board.RandomEmojiBoard(10, 10);
        await Board.ParseFromFile($"Boards/data/{boardFile}");
        Console.WriteLine(Board.Instance);

        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddCors(o =>
            o.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

        var app = builder.Build();
        app.UseCors();

        app.MapGet("/look/{playerId}", async (string playerId) => await Command.Look(Board.Instance, playerId));
        app.MapGet("/flip/{playerId}/{position}", async (string playerId, string position) =>
        {
            var coords = position.Split(',').Select(int.Parse).ToArray();
            try
            {
                string result = await Command.Flip(Board.Instance, playerId, coords[0], coords[1]);
                return Results.Text(result);
            }
            catch (Exception e)
            {
                return Results.Conflict(e.Message);
            }
        });

        app.MapGet("replace/{playerId}/{fromCard}/{toCard}", async (string playerId, string fromCard, string toCard) =>
            await Command.Map(Board.Instance, playerId, async oldVal => oldVal == fromCard ? toCard : oldVal));

        app.MapGet("/watch/{playerId}", async (string playerId) => await Command.Watch(Board.Instance, playerId));

        return app;
    }

    public static async Task Main(string[] args)
    {
        var app = await StartServerAsync(args);
        var url = "http://0.0.0.0:" + (args.Length > 0 ? int.Parse(args[0]) : 8080);
        await app.RunAsync(url);
    }
}