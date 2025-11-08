using MemoryScramble.Boards;

namespace MemoryScramble.Commands;

public static class Command
{
    public static async Task<string> Flip(Board board, string playerId, int row, int column)
    {
        if (await board.Flip(row, column, playerId)) return await board.ToWatchString(playerId);
        throw new Exception("Nuh uh");
    }

    public static async Task<string> LeaderBoard(Board board)
    {
        return null;
    }
    
    public static async Task<string> Look(Board board, string playerId)
    { 
        return await board.ToWatchString(playerId);
    }
    
    public static async Task<string> Watch(Board board, string playerId)
    {
        return await board.Watch(playerId);
    }
    
}