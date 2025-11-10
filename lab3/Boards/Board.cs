using System.Collections.Concurrent;
using System.Text;
using Xunit;

namespace MemoryScramble.Boards;

public enum CardStatus { Down, Up, None }

public sealed class Card(string value, int row, int column)
{
    public string Value { get; set; } = value;
    public int Row => row;
    public int Column => column;
    public CardStatus Status { get; set; } = CardStatus.Down;
    public string? ControlledBy { get; set; } = null;
    public SemaphoreSlim _lock { get; } = new(1, 1);
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


/// <summary>
/// This class holds all the data about the board, including its cells, players and watchers
/// </summary>
public sealed class Board
{
    private static Board? _instance;
    public static Board Instance => _instance ?? throw new InvalidOperationException("Board not initialized. Call ParseFromFile first.");

    private readonly Card[,] _cards;
    public int Rows { get; }
    public int Cols { get; }

    public ConcurrentDictionary<string, Player> Players { get; } = new();
    public ConcurrentDictionary<string, TaskCompletionSource<string>> Watchers = new();

    private readonly SemaphoreSlim _lock = new(1, 1); // Board-wide lock
    private bool _boardModified;

    private Board(Card[,] cards)
    {
        Rows = cards.GetLength(0);
        Cols = cards.GetLength(1);
        _cards = cards;
        CheckRep();
    }
    
    /// <summary>
    /// Checks representation invariant for the board
    /// </summary>
    private void CheckRep()
    {
        
        // rows and columns are actually valid
        Assert.Equal(Rows, _cards.GetLength(0));
        Assert.Equal(Cols, _cards.GetLength(1));

        foreach (var card in _cards)
        {
            // valid card status
            Assert.True(Enum.IsDefined(typeof(CardStatus), card.Status), $"Invalid status for card at [{card.Row},{card.Column}]");
            
            // player which controls the card exists
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

    
    /// <summary>
    /// Finishes the turn. Needs to be called before the first card in the new turn
    /// </summary>
    /// <param name="player"></param>
    private void FinishPreviousTurn(Player player)
    {
        if (player.LastFirstCard != null)
        {
            // both cards
            if (player.LastSecondCard != null)
            {
                player.LastFirstCard._lock.WaitAsync();
                player.LastSecondCard._lock.WaitAsync();
                
                bool isMatch = player.LastFirstCard.Value == player.LastSecondCard.Value;
                
                player.LastSecondCard._lock.Release();
                player.LastFirstCard._lock.Release();
                
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

    /// <summary>
    /// Self explainatory, isn't it
    /// </summary>
    /// <param name="card"></param>
    private void TurnDownIfUncontrolled(Card card)
    {
        if (card.Status == CardStatus.Up && card.ControlledBy == null)
        {
            card.Status = CardStatus.Down;
            SetModified();
            _ = NotifyWatchers();
        }
    }

    /// <summary>
    /// Checks if the selected area is invalid to flip due to emptiness
    /// </summary>
    /// <param name="row">Row</param>
    /// <param name="column">Column</param>
    /// <returns>True of row, column out of border or the card status is None</returns>
    private bool IsEmptySpace(int row, int column)
    {
        if (row < 0 || row >= Rows || column < 0 || column >= Cols) return true;
        return _cards[row, column].Status == CardStatus.None;
    }
    
    /// <summary>
    /// Main wrapper method for flipping a specific cell. See <see cref="_flip"/>
    /// </summary>
    /// <param name="row">The number of the row of the cell</param>
    /// <param name="column">The number of the column of the cell</param>
    /// <param name="playerId">Player who executes the action</param>
    /// <returns>True if flip is successfull, otherwise false</returns>
    public async Task<bool> Flip(int row, int column, string playerId)
    {
        var (success, mod) = await _flip(row, column, playerId);
        if (mod) await NotifyWatchers();
        return success;
    }

    /// <summary>
    /// Method for actually performing the flip of the cell
    /// </summary>
    /// <param name="row">The number of the row of the cell</param>
    /// <param name="column">The number of the column of the cell</param>
    /// <param name="playerId">Player who executes the action</param>
    /// <returns>
    /// <para>
    /// <c>Success</c>: <c>true</c> if the cell flip was attempted and executed; 
    /// <c>false</c> if the coordinates were invalid or the action was blocked.
    /// </para>
    /// <para>
    /// <c>BoardModified</c>: <c>true</c> if one or more cells were successfully 
    /// flipped on the board; otherwise, <c>false</c>.
    /// </para>
    /// </returns>
    public async Task<(bool Success, bool BoardModified)> _flip(int row, int col, string playerId)
    {
        _boardModified = false;
        var player = Players.GetOrAdd(playerId, id => new Player(id));
        if (row < 0 || row >= Rows || col < 0 || col >= Cols) return (false, _boardModified);
        Card card = _cards[row, col];

        await _lock.WaitAsync();
        try
        {
            if (player.FirstCard == null) // No first card => this is the first card
            {
                FinishPreviousTurn(player); 
                
                if (IsEmptySpace(row, col)) return (false, _boardModified); // identified empty space: 

                // if card status is up, control it
                if (card.Status == CardStatus.Down)
                {
                    card.ControlledBy = playerId;
                    card.Status = CardStatus.Up;
                    player.FirstCard = card;
                    SetModified();
                    return (true, _boardModified);
                }
                else if (card.ControlledBy == null) // if no one controls the card, control it
                {
                    card.ControlledBy = playerId;
                    player.FirstCard = card;
                    SetModified();
                    return (true, _boardModified);
                }
                else
                {
                    // if someone controls the card, wait in the queue
                    var tcs = new TaskCompletionSource<bool>();
                    card.WaitQueue.Enqueue(tcs);

                    _lock.Release();
                    bool acquired = await tcs.Task;
                    await _lock.WaitAsync();

                    // if successfully waited, control the card
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
            else // if it's the second card opened
            {
                // if same card as first, lose control 
                if (player.FirstCard == card)  
                {
                    player.LastFirstCard = player.FirstCard;
                    LoseControl(player.FirstCard, false);
                    player.FirstCard = null;
                    // SetModified();
                    return (false, _boardModified);
                }

                // if identified empty space
                if (card.Status == CardStatus.None)
                {
                    player.LastFirstCard = player.FirstCard;
                    LoseControl(player.FirstCard, false);
                    player.FirstCard = null;
                    return (false, false);
                }
                
                // to avoid dedlocks, if card is controlled, lose control of both
                if (card.Status == CardStatus.Up && card.ControlledBy != null)
                {
                    LoseControl(player.FirstCard, false);
                    player.LastFirstCard = player.FirstCard;
                    player.FirstCard = null;
                    SetModified();
                    return (false, _boardModified);
                }
                
                _lock.Release();
                await card._lock.WaitAsync();
                
                // if not controlled, try to control
                try
                {
                    string cardValue = card.Value;
                    
                    // flip the card if down
                    if (card.Status == CardStatus.Down)
                    {
                        card.Status = CardStatus.Up;
                        SetModified();
                    }

                    await _lock.WaitAsync();
                    
                    // if match, control both cards until the next turn
                    if (player.FirstCard.Value == cardValue)
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
                    
                    // if not match, lose control of both cards, reset the cards
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
                finally
                {
                    card._lock.Release();
                }
            }
        }
        finally
        {
            if (_lock.CurrentCount == 0) _lock.Release();
        }
    }

    /// <summary>
    /// Notifies the first waiter of the card that it's free
    /// </summary>
    /// <param name="card">The card which became free</param>
    private void InformCardFree(Card card)
    {
        if (card.WaitQueue.TryDequeue(out var tcs))
        {
            tcs.SetResult(true);
        }
    }

    /// <summary>
    /// Informs all the waiters about the status of the card (used to deny access)
    /// </summary>
    /// <param name="card">The card which status we share</param>
    /// <param name="value">Status of the operation</param>
    private void InformAllWaiters(Card card, bool value = false)
    {
        while (card.WaitQueue.TryDequeue(out var tcs))
        {
            tcs.TrySetResult(value);
        }
    }

    /// <summary>
    /// Make the card lose its owner and informing the next one in the queue that it's free
    /// </summary>
    /// <param name="card"></param>
    /// <param name="turnDown"></param>
    private void LoseControl(Card? card, bool turnDown)
    {
        if (card != null)
        {
            card.ControlledBy = null;
            InformCardFree(card);
        }
    }

    /// <summary>
    /// Gives the string which represents the board state from the perspective of the player
    /// </summary>
    /// <param name="playerId"></param>
    /// <returns></returns>
    public async Task<string> ToWatchString(string playerId)
    {
        var viewSnapshot = new (CardStatus Status, string Value, string ControlledBy)[Rows, Cols];

        await _lock.WaitAsync();
        try
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    var card = _cards[r, c];
                    viewSnapshot[r, c] = (card.Status, card.Value, card.ControlledBy);
                }
            }
        }
        finally
        {
            _lock.Release();
        }

        var sb = new StringBuilder();
        sb.Append($"{Rows}x{Cols}\n");
    
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                var (status, value, controlledBy) = viewSnapshot[r, c];

                sb.Append(status switch
                {
                    CardStatus.None => "none",
                    CardStatus.Down => "down",
                    // Access 'value' and 'controlledBy' from the snapshot
                    CardStatus.Up when controlledBy == playerId => $"my {value}",
                    CardStatus.Up => $"up {value}",
                    _ => "down"
                });
                sb.Append('\n');
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Adds the player to a list of watchers and returns result when the board is updated
    /// </summary>
    /// <param name="playerId">Player who is watching the board</param>
    /// <returns>View of the board from the perspective of the player</returns>
    public async Task<string> Watch(string playerId)
    {
        var player = Players.GetOrAdd(playerId, id => new Player(id));
        var watcher = new TaskCompletionSource<string>();
        Watchers[playerId] = watcher;
        return await watcher.Task;
    }

    /// <summary>
    /// Tell all the watchers the board is updated. Their wait task return their view
    /// </summary>
    private async Task NotifyWatchers()
    {
        foreach (var kvp in Watchers.ToArray())
        {
            kvp.Value.TrySetResult(await ToWatchString(kvp.Key));
        }

        _boardModified = false;
        Watchers.Clear();
    }
    
    /// <summary>
    /// Applies the transformation function <c>f</c> to every card on the board
    /// </summary>
    /// <param name="playerId">player who executes the action</param>
    /// <param name="f">transformation function</param>
    /// <returns>View of the board from the perspective of the mapper</returns>
    public async Task<string> Map(string playerId, Func<string, Task<string>> f)
    {
        
        // makes the snapshot of the board
        IReadOnlyList<(string Value, Card Card)> snapshot;
        await _lock.WaitAsync();
        try
        {
            snapshot = _cards.Cast<Card>()
                             .Where(c => c.Status != CardStatus.None)
                             .Select(c => (Value: c.Value, Card: c))
                             .ToList();
        }
        finally { _lock.Release(); }

        // combines the groups <value, cardList>
        var groups = snapshot
            .GroupBy(t => t.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        _boardModified = false;

        // calculate the value of f for each value
        foreach (var kv in groups)
        {
            var (oldVal, tuples) = (kv.Key, kv.Value);

            var newVal = await f(oldVal);
            
            if (oldVal == newVal)
                continue;
            
            SetModified();
            
            var cards = tuples.Select(t => t.Card).ToList();

            // updates the values for that cards in the group
            await _lock.WaitAsync();
            try
            {
                foreach (var card in cards)
                    card.Value = newVal;
            } 
            finally { _lock.Release(); }
            
        }

        if (_boardModified)
            await NotifyWatchers();

        return await ToWatchString(playerId);
    }

    public static Task<Board> RandomEmojiBoard(int rows, int cols)
    {
        List<string> emojis = new List<string>
        {
            "🍎", "🍌", "🍕", "🍔", "🍟", "🌲", "🌸", "🌞", "🌛", "⭐",
            "⚡", "🔥", "💧", "🌊", "🍩", "🍪", "🎂", "🍫", "🍬", "🍭",
            "🚗", "🚀", "✈️", "🚢", "🚲", "🛴", "🏠", "🏰", "🗿", "🎁",
            "🎈", "🎨", "🎸", "🎹", "📱", "💻", "⌚", "📷", "🔑", "💡",
            "🛒", "🛏️", "🛋️", "🪑", "🕯️", "📚", "📖", "🖼️", "🧭", "🔨"
        };

        var rand = new Random();
        var cards = new Card[rows, cols];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                string emoji = emojis[rand.Next(emojis.Count)];
                cards[r, c] = new Card(emoji, r, c);
            }
        }

        _instance = new Board(cards);
        return Task.FromResult(_instance);
    }

    
    public static async Task<Board> ParseFromFile(string relativeFilename)
    {
        var path = Path.Combine("Boards/data", relativeFilename);
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