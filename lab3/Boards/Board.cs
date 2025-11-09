using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MemoryScramble.Boards;

public enum CardStatus { Down, Up, None }

public sealed record Card(string value, int Row, int Column)
{
    public string Value = value;
    public CardStatus Status { get; set; } = CardStatus.Down;
    public string? ControlledBy { get; set; } = null;
    public Queue<TaskCompletionSource<bool>> WaitQueue { get; } = new();
}

public sealed class Player
{
    public string Id { get; }
    public Card? FirstCard { get; set; }
    public Card? SecondCard { get; set; }
    public Card? LastFirstCard { get; set; }
    public Card? LastSecondCard { get; set; }

    public Player(string id) => Id = id;
}

public sealed class Board
{
    private static Board? _instance;
    public static Board Instance => _instance ?? throw new InvalidOperationException("Board not initialized. Call ParseFromFile first.");

    private readonly Card[,] _cards;
    public int Rows { get; }
    public int Cols { get; }

    public ConcurrentDictionary<string, Player> Players { get; } = new();
    public ConcurrentDictionary<string, TaskCompletionSource<string>> Watchers = new();

    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _boardModified;

    private Board(Card[,] cards)
    {
        Rows = cards.GetLength(0);
        Cols = cards.GetLength(1);
        _cards = cards;
        CheckRep();
    }

    private void CheckRep()
    {
        Assert.Equal(Rows, _cards.GetLength(0));
        Assert.Equal(Cols, _cards.GetLength(1));
        
        foreach (var card in _cards)
        {
            Assert.True(Enum.IsDefined(typeof(CardStatus), card.Status), $"Invalid status for card at [{card.Row},{card.Column}]");
            if (card.ControlledBy != null)
                Assert.Contains(card.ControlledBy, Players.Keys);
        }

        var controlledCards = new HashSet<Card>();
        foreach (var player in Players.Values)
        {
            if (player.FirstCard != null)
            {
                Assert.Same(_cards[player.FirstCard.Row, player.FirstCard.Column], player.FirstCard);
                Assert.True(controlledCards.Add(player.FirstCard), $"Card at [{player.FirstCard.Row},{player.FirstCard.Column}] controlled by multiple players");
            }
            if (player.SecondCard != null)
            {
                Assert.Same(_cards[player.SecondCard.Row, player.SecondCard.Column], player.SecondCard);
                Assert.True(controlledCards.Add(player.SecondCard), $"Card at [{player.SecondCard.Row},{player.SecondCard.Column}] controlled by multiple players");
            }
            if (player.LastFirstCard != null)
            {
                Assert.Same(_cards[player.LastFirstCard.Row, player.LastFirstCard.Column], player.LastFirstCard);
            }
            if (player.LastSecondCard != null)
            {
                Assert.Same(_cards[player.LastSecondCard.Row, player.LastSecondCard.Column], player.LastSecondCard);
            }
        }

        foreach (var card in _cards)
        {
            Assert.NotNull(card.WaitQueue);
        }
    }

    
    private void SetModified() => _boardModified = true;

    private void FinishPreviousTurn(Player player)
    {
        if (player.LastFirstCard != null)
        {
            if (player.LastSecondCard != null)
            {
                bool isMatch = player.LastFirstCard.Value == player.LastSecondCard.Value;
                if (isMatch)
                {
                    player.LastFirstCard.Status = CardStatus.None;
                    player.LastSecondCard.Status = CardStatus.None;
                    SetModified();

                    InformAllWaiters(player.LastFirstCard, false);
                    InformAllWaiters(player.LastSecondCard, false);

                    LoseControl(player.LastSecondCard, false);
                    LoseControl(player.LastFirstCard, false);
                }
                else
                {
                    TurnDownIfUncontrolled(player.LastFirstCard);
                    TurnDownIfUncontrolled(player.LastSecondCard);
                }

                player.LastFirstCard = null;
                player.LastSecondCard = null;
            }
            else
            {
                TurnDownIfUncontrolled(player.LastFirstCard);
                player.LastFirstCard = null;
                player.LastSecondCard = null;
            }
        }
    }

    private void TurnDownIfUncontrolled(Card card)
    {
        if (card.Status == CardStatus.Up && card.ControlledBy == null)
        {
            card.Status = CardStatus.Down;
            SetModified();
            _ = NotifyWatchers();
        }
    }

    private bool IsEmptySpace(int row, int column)
    {
        if (row < 0 || row >= Rows || column < 0 || column >= Cols) return true;
        return _cards[row, column].Status == CardStatus.None;
    }

    public async Task<bool> Flip(int row, int column, string playerId)
    {
        var (success, mod) = await _flip(row, column, playerId);
        if (mod) await NotifyWatchers();
        return success;
    }
    
    public async Task<(bool Success, bool BoardModified)> _flip(int row, int col, string playerId)
    {
        _boardModified = false;
        var player = Players.GetOrAdd(playerId, id => new Player(id));
        Card card = _cards[row, col];

        await _lock.WaitAsync();
        try
        {
            if (player.FirstCard == null)
            {
                FinishPreviousTurn(player);

                if (IsEmptySpace(row, col)) return (false, _boardModified);

                if (card.Status == CardStatus.Down)
                {
                    card.ControlledBy = playerId;
                    card.Status = CardStatus.Up;
                    player.FirstCard = card;
                    SetModified();
                    return (true, _boardModified);
                }
                else if (card.ControlledBy == null)
                {
                    card.ControlledBy = playerId;
                    player.FirstCard = card;
                    SetModified();
                    return (true, _boardModified);
                }
                else
                {
                    var tcs = new TaskCompletionSource<bool>();
                    card.WaitQueue.Enqueue(tcs);

                    _lock.Release();
                    bool acquired = await tcs.Task;
                    await _lock.WaitAsync();

                    if (acquired)
                    {
                        card.ControlledBy = playerId;
                        player.FirstCard = card;
                        SetModified();
                        return (true, _boardModified);
                    }
                    else
                    {
                        return (false, _boardModified);
                    }
                }
            }
            else
            {
                if (player.FirstCard == card)
                {
                    player.LastFirstCard = player.FirstCard;
                    LoseControl(player.FirstCard, false);
                    player.FirstCard = null;
                    SetModified();
                    return (false, _boardModified);
                }

                if (card.Status == CardStatus.Up && card.ControlledBy != null)
                {
                    LoseControl(player.FirstCard, false);
                    player.LastFirstCard = player.FirstCard;
                    player.FirstCard = null;
                    SetModified();
                    return (false, _boardModified);
                }

                if (card.Status == CardStatus.Down)
                {
                    card.Status = CardStatus.Up;
                    SetModified();
                }

                if (player.FirstCard.Value == card.Value)
                {
                    card.ControlledBy = playerId;
                    player.SecondCard = card;

                    player.LastFirstCard = player.FirstCard;
                    player.LastSecondCard = player.SecondCard;

                    player.FirstCard = null;
                    player.SecondCard = null;
                    SetModified();
                    return (true, _boardModified);
                }
                else
                {
                    player.SecondCard = card;

                    LoseControl(player.FirstCard, false);
                    LoseControl(player.SecondCard, false);

                    player.LastFirstCard = player.FirstCard;
                    player.LastSecondCard = player.SecondCard;

                    player.FirstCard = null;
                    player.SecondCard = null;
                    SetModified();
                    return (true, _boardModified);
                }
            }
        }
        finally
        {
            if (_lock.CurrentCount == 0) _lock.Release();
        }
    }

    private void InformCardFree(Card card)
    {
        if (card.WaitQueue.TryDequeue(out var tcs))
        {
            tcs.SetResult(true);
        }
    }

    private void InformAllWaiters(Card card, bool value = false)
    {
        while (card.WaitQueue.TryDequeue(out var tcs))
        {
            tcs.TrySetResult(value);
        }
    }

    private void LoseControl(Card? card, bool turnDown)
    {
        if (card != null)
        {
            card.ControlledBy = null;
            InformCardFree(card);
            SetModified();
        }
    }

    public async Task<string> ToWatchString(string playerId)
    {
        var sb = new StringBuilder();
        sb.Append($"{Rows}x{Cols}\n");

        await _lock.WaitAsync();
        try
        {
            for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                var card = _cards[r, c];
                sb.Append(card.Status switch
                {
                    CardStatus.None => "none",
                    CardStatus.Down => "down",
                    CardStatus.Up when card.ControlledBy == playerId => $"my {card.Value}",
                    CardStatus.Up => $"up {card.Value}",
                    _ => "down"
                });
                sb.Append('\n');
            }
        }
        finally
        {
            _lock.Release();
        }

        return sb.ToString();
    }

    public async Task<string> Watch(string playerId)
    {
        var player = Players.GetOrAdd(playerId, id => new Player(id));
        var watcher = new TaskCompletionSource<string>();
        Watchers[playerId] = watcher;
        return await watcher.Task;
    }

    private async Task NotifyWatchers()
    {
        foreach (var kvp in Watchers.ToArray())
        {
            kvp.Value.TrySetResult(await ToWatchString(kvp.Key));
        }
        Watchers.Clear();
    }

    public async Task<string> Map(string playerId, Func<string,Task<string>> f)
    {
        List<Card> snapshot;
        await _lock.WaitAsync();
        try {
            snapshot = _cards.Cast<Card>().Where(c=>c.Status!=CardStatus.None).ToList();
        }
        finally { _lock.Release(); }

        var tasks = snapshot.Select(async card => {
            var newVal = await f(card.Value); 

            await _lock.WaitAsync();
            try {
                card.Value = newVal;
                SetModified();
            }
            finally { _lock.Release(); }
        }).ToList();

        await Task.WhenAll(tasks); 

        return await ToWatchString(playerId);
    }

    
    public static async Task<Board> ParseFromFile(string relativeFilename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", relativeFilename);
        var lines = await File.ReadAllLinesAsync(path);

        if (lines.Length == 0) throw new InvalidOperationException("Board file is empty.");

        var dim = lines[0].Split('x');
        if (dim.Length != 2) throw new InvalidOperationException("First line must be 'rowsxcols'.");

        int rows = int.Parse(dim[0]);
        int cols = int.Parse(dim[1]);

        if (lines.Length != 1 + rows * cols)
            throw new InvalidOperationException($"Expected {rows * cols} card lines, got {lines.Length - 1}.");

        var cards = new Card[rows, cols];
        int idx = 1;
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            var value = lines[idx++].Trim();
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Empty card at [{r},{c}].");
            cards[r, c] = new Card(value, r, c);
        }

        _instance = new Board(cards);
        return _instance;
    }
}
