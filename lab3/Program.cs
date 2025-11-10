using MemoryScramble.Boards;
using MemoryScramble.Commands;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

public static class Program
{
    public static async Task StartServerAsync(Board board, int serverPort) 
    {
        
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddCors(o =>
            o.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

        var app = builder.Build();
        app.UseCors();

        app.MapGet("/look/{playerId}", async (string playerId) => await Command.Look(board, playerId));
        app.MapGet("/flip/{playerId}/{position}", async (string playerId, string position) =>
        {
            var coords = position.Split(',').Select(int.Parse).ToArray();
            try
            {
                string result = await Command.Flip(board, playerId, coords[0], coords[1]);
                return Results.Text(result);
            }
            catch (Exception e)
            {
                return Results.Conflict(e.Message);
            }
        });

        app.MapGet("replace/{playerId}/{fromCard}/{toCard}", async (string playerId, string fromCard, string toCard) =>
            await Command.Map(board, playerId, async oldVal => oldVal == fromCard ? toCard : oldVal));
        
        app.MapGet("/watch/{playerId}", async (string playerId) => await Command.Watch(board, playerId));
        
        
        app.MapGet("/", async () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, ".frontend", "index.html");
            var html = await File.ReadAllTextAsync(path);
            return Results.Content(html, "text/html");
        });
        
        var url = "http://0.0.0.0:" + serverPort;
        await app.RunAsync(url);
    }

    public static async Task Main(string[] args)
    {
        var serverPort = args.Length > 0 ? int.Parse(args[0]) : 8080;
        var boardFile = args.Length > 1 ? args[1] : "zoom.txt";
        var board = await Board.ParseFromFile($"{boardFile}");
        
        await StartServerAsync(board, serverPort);
    }
}