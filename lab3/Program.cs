using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

public class Program
{
    public static void Main(string[] args)
    {   
        
        var serverPort = args.Length > 0 ? int.Parse(args[0]) : 8080;
        Board.ParseFromFile("Boards/ab.txt");
        
        Console.WriteLine(Board.Instance);
        
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();
        
        app.MapGet("/{playerId}/{position}", () => "Hello from server"); // responds to GET /
        
        var url = "http://localhost:" + serverPort;
        
        app.Run(url);
        
    }
}

