using System;
using System.Reflection;
using MemoryScramble.Boards;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace MemoryScramble.Tests;

public class BoardTest
{
    [Fact]
    public async void Identified_Empty_Space()
    {
        var board = await Board.ParseFromFile("../Boards/data/ab.txt");
        Assert.True(await board.Flip(0,0, "ryangosling")); // it's A
        Assert.True(await board.Flip(0, 2, "ryangosling")); // it's other A
        Assert.True(await board.Flip(0, 3, "ryangosling")); // it's other B
        Assert.False(await board.Flip(0, 0, "ryangosling")); // it's the first A => already none => false
        
    }
    
    [Fact]
    public async void IfFaceDown_FaceUp()
    {
        var board = await Board.ParseFromFile("../Boards/data/ab.txt");
        
        var cards = GetCardsWithReflection(board);
        Assert.True(cards[0,0].Status == CardStatus.Down);
        Assert.True(await board.Flip(0,0, "ryangosling")); 
        Assert.True(cards[0,0].Status == CardStatus.Up);
        
    }

    public Card[,] GetCardsWithReflection(Board board)
    {
        var cardsInfo = typeof(Board).GetField("_cards", BindingFlags.NonPublic | BindingFlags.Instance);
        return cardsInfo.GetValue(board) as Card[,];
    }
}
