using System.Text;

namespace MemoryScramble.Boards;

public class LeaderBoard
{
    private Dictionary<string, int> _values = new();
    
    public String ViewSorted()
    {
        var values = _values.ToArray().OrderBy(i => i.Value);

        StringBuilder sb = new();
        foreach (var value in values)
        {
            sb.Append($"{value.Key} {value.Value}\n");
        }
        return sb.ToString();
    }
}
