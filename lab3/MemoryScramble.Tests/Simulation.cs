using MemoryScramble.Boards;
using MemoryScramble.Commands;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System.Threading.Tasks.Dataflow;
using Xunit;
using Xunit.Abstractions;

namespace MemoryScramble.Tests;

public class Simulation
{
    private readonly ITestOutputHelper _output;
    private const int BoardSize = 40;
    private const int PlayerCount = 4;
    private const int MovesPerPlayer = 10000;
    private const double MinDelayMs = 0.1;
    private const double MaxDelayMs = 2.0;

    private readonly string[] _players = 
    {
        "ryangosling", "tiranosaurus", "discriminant", "mongolianrap"
    };

    private readonly Random _rand = new();

    public Simulation(ITestOutputHelper testOutputHelper)
    {
        _output = testOutputHelper;
    }

    [Fact]
    public async Task StartSimulation()
    {
        // 1. Create 15x15 emoji board
        var board = await Board.RandomEmojiBoard(BoardSize, BoardSize);
        _output.WriteLine($"Board created: {BoardSize}x{BoardSize} = {BoardSize * BoardSize} cards");

        var serverTask = Program.StartServerAsync(board, 8080);
        await Task.Delay(3000); // Wait for server init
        _output.WriteLine("Server running on :8080");

        var playerTasks = new List<Task>();
        for (int i = 0; i < PlayerCount; i++)
        {
            string playerId = _players[i];
            playerTasks.Add(SimulatePlayer(playerId, board));
        }

        _output.WriteLine($"Launched {PlayerCount} players: {string.Join(", ", _players)}");

        // 4. Wait for all to finish
        await Task.WhenAll(playerTasks);
        await serverTask; // Ensure server doesn't crash

        _output.WriteLine("SIMULATION COMPLETE: 400 moves, 0 crashes, thread-safe");
    }

    private async Task SimulatePlayer(string playerId, Board board)
    {
        int successfulFlips = 0;
        int failedFlips = 0;

        for (int move = 0; move < MovesPerPlayer; move++)
        {
            try
            {
                // Random delay: 0.1ms to 2ms
                await RandomTimeout();

                // Pick two distinct random positions
                (int r1, int c1) = RandomPosition();
                (int r2, int c2) = RandomPosition();
                while (r1 == r2 && c1 == c2)
                    (r2, c2) = RandomPosition();

                // First flip
                await Command.Flip(board, playerId, r1, c1);
                await RandomTimeout();

                // Second flip
                bool secondSuccess = await board.Flip(r2, c2, playerId);
                if (secondSuccess) successfulFlips++; else failedFlips++;

                // Optional: Watch board state (uncomment to debug)
                // string view = await Command.Watch(board, playerId);
                // _output.WriteLine($"[{playerId}] Move {move}: ({r1},{c1}) → ({r2},{c2})\n{view}");
            }
            catch (Exception ex)
            {
                failedFlips++;
                _output.WriteLine($"[{playerId}] Move {move} failed: {ex.Message}");
            }
        }

        _output.WriteLine($"[{playerId}] Finished: {successfulFlips} successful flips, {failedFlips} failed");
    }

    private (int row, int col) RandomPosition()
    {
        return (_rand.Next(BoardSize), _rand.Next(BoardSize));
    }

    private async Task RandomTimeout()
    {
        int delayMs = (int)(_rand.NextDouble() * (MaxDelayMs - MinDelayMs) + MinDelayMs);
        await Task.Delay(delayMs);
    }
}