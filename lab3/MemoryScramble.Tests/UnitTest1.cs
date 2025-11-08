using System;
using MemoryScramble.Boards;
using Xunit;

namespace MemoryScramble.Tests;

public class BoardTest
{
    [Fact]
    public void Instance_Throws_WhenNotInitialized()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => { _ = Board.Instance; });
        Assert.Contains("Board not initialized", ex.Message);
    }

    [Fact]
    public void Instance_Not_Throws_WhenInitialized()
    {
        var ex = Board.ParseFromFile("data/ab.txt");
        Assert.NotNull(ex);
    }
}
