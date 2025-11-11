// BoardRulesTests.cs
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MemoryScramble.Boards;
using Xunit;
using Xunit.Abstractions;

namespace MemoryScramble.Tests;

public class BoardRulesTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private Board _board = null!;

    public BoardRulesTests(ITestOutputHelper output) => _output = output;

    private async Task SetupBoardAsync(string content)
    {
        // Ensure Boards/data exists in test output directory
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Boards", "data");
        Directory.CreateDirectory(dataDir);

        // Create unique temp file inside Boards/data/
        var fileName = Path.GetRandomFileName(); // e.g. "abc123.tmp"
        var fullPath = Path.Combine(dataDir, fileName);

        await File.WriteAllTextAsync(fullPath, content);

        // Parse using just the filename — Board.ParseFromFile expects this
        _board = await Board.ParseFromFile(fileName);
    }

    public void Dispose() { /* nothing to clean up */ }

    // ------------------------------------------------------------------------
    // Helper: get a snapshot of the board from a player's view
    private async Task<string> View(string playerId) => await _board.ToWatchString(playerId);

    // Helper: reflect into private _cards (only for assertions, not for mutation)
    private Card[,] GetCards() =>
        (Card[,])typeof(Board)
            .GetField("_cards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(_board)!;

    // ------------------------------------------------------------------------
    // 1-A : Empty space -> fail
    [Fact]
    public async Task Rule_1A_EmptySpace_Fails()
    {
        // 1x3 board: A A B
        await SetupBoardAsync("1x3\nA\nA\nB");

        // remove first A (simulate previous match)
        var cards = GetCards();
        cards[0, 0].Status = CardStatus.None;

        bool flipped = await _board.Flip(0, 0, "p1");
        Assert.False(flipped);
        Assert.Equal(CardStatus.None, cards[0, 0].Status);
        Assert.Null(cards[0, 0].ControlledBy);
    }

    // 1-B : Face-down -> turn up + control
    [Fact]
    public async Task Rule_1B_FaceDown_TurnUp_Control()
    {
        await SetupBoardAsync("1x1\nA");
        var cards = GetCards();

        bool flipped = await _board.Flip(0, 0, "p1");
        Assert.True(flipped);
        Assert.Equal(CardStatus.Up, cards[0, 0].Status);
        Assert.Equal("p1", cards[0, 0].ControlledBy);
    }

    // 1-C : Face-up, uncontrolled -> control (no flip)
    [Fact]
    public async Task Rule_1C_FaceUp_Uncontrolled_Control()
    {
        await SetupBoardAsync("1x1\nA");
        var cards = GetCards();
        cards[0, 0].Status = CardStatus.Up;
        cards[0, 0].ControlledBy = null;

        bool flipped = await _board.Flip(0, 0, "p1");
        Assert.True(flipped);
        Assert.Equal(CardStatus.Up, cards[0, 0].Status);
        Assert.Equal("p1", cards[0, 0].ControlledBy);
    }

    // 1-D : Face-up, controlled by other -> wait in queue
    [Fact]
    public async Task Rule_1D_FaceUp_ControlledByOther_Waits()
    {
        await SetupBoardAsync("1x1\nA");
        var cards = GetCards();
        cards[0, 0].Status = CardStatus.Up;
        cards[0, 0].ControlledBy = "p2";

        // p1 tries to flip -> should enqueue
        var flipTask = _board.Flip(0, 0, "p1");

        // give scheduler a chance
        await Task.Delay(50);
        Assert.False(flipTask.IsCompleted);

        // p2 releases control
        cards[0, 0].ControlledBy = null;
        // Inform waiter (normally done by LoseControl)
        while (cards[0, 0].WaitQueue.TryDequeue(out var tcs)) tcs.SetResult(true);

        bool result = await flipTask;
        Assert.True(result);
        Assert.Equal("p1", cards[0, 0].ControlledBy);
    }

    // ------------------------------------------------------------------------
    // 2-A : Second card empty -> fail + lose first control
    [Fact]
    public async Task Rule_2A_SecondCard_Empty_Fail_LoseFirst()
    {
        await SetupBoardAsync("1x2\nA\nA");
        var cards = GetCards();

        // p1 takes first card
        await _board.Flip(0, 0, "p1");
        Assert.Equal("p1", cards[0, 0].ControlledBy);

        // remove second card
        cards[0, 1].Status = CardStatus.None;

        bool flipped = await _board.Flip(0, 1, "p1");
        Assert.False(flipped);
        Assert.Null(cards[0, 0].ControlledBy); // lost control
        Assert.Equal(CardStatus.Up, cards[0, 0].Status); // still up
    }

    // 2-B : Second card controlled -> fail + lose first control
    [Fact]
    public async Task Rule_2B_SecondCard_Controlled_Fail_LoseFirst()
    {
        await SetupBoardAsync("1x2\nA\nB");
        var cards = GetCards();

        await _board.Flip(0, 0, "p1"); // p1 controls first
        cards[0, 1].Status = CardStatus.Up;
        cards[0, 1].ControlledBy = "p2";

        bool flipped = await _board.Flip(0, 1, "p1");
        Assert.False(flipped);
        Assert.Null(cards[0, 0].ControlledBy); // lost
        Assert.Equal(CardStatus.Up, cards[0, 0].Status);
    }

    // 2-C : Second card face-down -> turn up
    [Fact]
    public async Task Rule_2C_SecondCard_FaceDown_TurnUp()
    {
        await SetupBoardAsync("1x2\nA\nB");
        var cards = GetCards();

        await _board.Flip(0, 0, "p1");
        bool flipped = await _board.Flip(0, 1, "p1");
        Assert.True(flipped);
        Assert.Equal(CardStatus.Up, cards[0, 1].Status);
    }

    // 2-D : Match -> keep control of both
    [Fact]
    public async Task Rule_2D_Match_KeepControl()
    {
        await SetupBoardAsync("1x2\nA\nA");
        var cards = GetCards();

        await _board.Flip(0, 0, "p1");
        await _board.Flip(0, 1, "p1");

        Assert.Equal("p1", cards[0, 0].ControlledBy);
        Assert.Equal("p1", cards[0, 1].ControlledBy);
        Assert.Equal(CardStatus.Up, cards[0, 0].Status);
        Assert.Equal(CardStatus.Up, cards[0, 1].Status);
    }

    // 2-E : No match -> lose control of both (remain up)
    [Fact]
    public async Task Rule_2E_NoMatch_LoseControl_RemainUp()
    {
        await SetupBoardAsync("1x2\nA\nB");
        var cards = GetCards();

        await _board.Flip(0, 0, "p1");
        await _board.Flip(0, 1, "p1");

        Assert.Null(cards[0, 0].ControlledBy);
        Assert.Null(cards[0, 1].ControlledBy);
        Assert.Equal(CardStatus.Up, cards[0, 0].Status);
        Assert.Equal(CardStatus.Up, cards[0, 1].Status);
    }

    // ------------------------------------------------------------------------
    // 3-A : After match -> next first flip removes both cards
    [Fact]
    public async Task Rule_3A_Match_RemovedOnNextTurn()
    {
        await SetupBoardAsync("1x3\nA\nA\nB");
        var cards = GetCards();

        // Match A-A
        await _board.Flip(0, 0, "p1");
        await _board.Flip(0, 1, "p1");

        // Now p1 starts new turn: flip B
        await _board.Flip(0, 2, "p1");

        Assert.Equal(CardStatus.None, cards[0, 0].Status);
        Assert.Equal(CardStatus.None, cards[0, 1].Status);
        Assert.Null(cards[0, 0].ControlledBy);
        Assert.Null(cards[0, 1].ControlledBy);
    }

    // 3-B : After mismatch -> next first flip turns down uncontrolled up cards
    [Fact]
    public async Task Rule_3B_Mismatch_TurnDownUncontrolled()
    {
        await SetupBoardAsync("1x3\nA\nB\nC");
        var cards = GetCards();

        await _board.Flip(0, 0, "p1"); // A
        await _board.Flip(0, 1, "p1"); // B -> mismatch

        // p2 sees them up, does nothing
        // p1 starts new turn
        await _board.Flip(0, 2, "p1"); // C

        Assert.Equal(CardStatus.Down, cards[0, 0].Status);
        Assert.Equal(CardStatus.Down, cards[0, 1].Status);
    }

    // 3-B edge: only turn down if still uncontrolled
    [Fact]
    public async Task Rule_3B_OnlyUncontrolled_TurnDown()
    {
        await SetupBoardAsync("1x3\nA\nB\nA");
        var cards = GetCards();

        await _board.Flip(0, 0, "p1"); // A
        await _board.Flip(0, 1, "p1"); // B -> mismatch

        // p2 grabs first A before p1 starts new turn
        await _board.Flip(0, 0, "p2");

        // p1 starts new turn
        await _board.Flip(0, 2, "p1"); // second A

        Assert.Equal(CardStatus.Up, cards[0, 0].Status); // p2 controls it
        Assert.Equal("p2", cards[0, 0].ControlledBy);
        Assert.Equal(CardStatus.Down, cards[0, 1].Status); // B was uncontrolled -> down
    }

    // ------------------------------------------------------------------------
    // Concurrency: other players play while one waits
    [Fact]
    public async Task Concurrency_OtherPlayersPlay_WhileOneWaits()
    {
        await SetupBoardAsync("1x3\nA\nB\nC");
        var cards = GetCards();

        // p1 takes A (up, controlled)
        await _board.Flip(0, 0, "p1");

        // p2 tries to take A -> waits
        var p2Task = _board.Flip(0, 0, "p2");
        await Task.Delay(50);
        Assert.False(p2Task.IsCompleted);

        // p3 plays on B and C
        await _board.Flip(0, 1, "p3"); // B up
        await _board.Flip(0, 2, "p3"); // C up

        // p1 releases A
        cards[0, 0].ControlledBy = null;
        while (cards[0, 0].WaitQueue.TryDequeue(out var tcs)) tcs.SetResult(true);

        bool p2GotIt = await p2Task;
        Assert.True(p2GotIt);
        Assert.Equal("p2", cards[0, 0].ControlledBy);
    }
    
}