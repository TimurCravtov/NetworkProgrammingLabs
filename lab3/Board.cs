using System;
using System.IO;
using System.Linq;

public class Board
{
    private static Board _instance;

    private string[,] _board;

    private Board(string[,] board)
    {
        _board = board;
    }

    /// <summary>
    /// Access the singleton instance
    /// </summary>
    public static Board Instance
    {
        get
        {
            if (_instance == null)
                throw new Exception("Board instance has not been initialized. Call ParseFromFile first.");
            return _instance;
        }
    }

    /// <summary>
    /// Parse a board from file and set the singleton instance
    /// </summary>
    public static Board ParseFromFile(string relativeFilename)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", relativeFilename);

            using var sr = new StreamReader(path);

            var dimensionLine = sr.ReadLine();
            if (dimensionLine == null)
                throw new Exception("File is empty or missing dimensions line.");

            var dimensions = dimensionLine.Split('x').Select(int.Parse).ToArray();
            if (dimensions.Length != 2)
                throw new Exception("Dimensions line must be in format 'rowsxcolumns'.");

            int rows = dimensions[0];
            int cols = dimensions[1];
            string[,] board = new string[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    var line = sr.ReadLine();
                    if (line == null)
                        throw new Exception($"Unexpected end of file while reading row {i}, column {j}.");
                    board[i, j] = line;
                }
            }

            _instance = new Board(board);
            return _instance;
        }
        catch (Exception e)
        {
            throw new Exception("Board failed to initialize. Check the board formatting.", e);
        }
    }

    public override string ToString()
    {
        int rows = _board.GetLength(0);
        int cols = _board.GetLength(1);
        var result = "";

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                result += _board[i, j] + (j < cols - 1 ? " " : "");
            }
            result += Environment.NewLine;
        }

        return result;
    }
}
