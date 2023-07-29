using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ChessChallenge.API;

public class DebugBot : MyBot, IChessBot
{
    public const bool Enabled = false;
    bool didInit = false;

    int statsNodes = 0;
    int statsQNodes = 0;
    int statsEvals = 0;
    double bestScore = 0;
    int[] statsSearch;

    public void Init()
    {
        didInit = true;
        Console.WriteLine("DebugBot");
        new ChessTables().Generate();
    }

    public static double TimeLimit(double timeLimit)
    {
        /* return timeLimit; */
        /* return 100; */
        return Enabled ? 10000 : 100;
    }

    public new Move Think(Board board, Timer timer)
    {
        if (!didInit)
            Init();

        statsSearch = new int[50 * 2 + 16];
        bestScore = 0;
        statsNodes = 0;
        statsQNodes = 0;
        statsEvals = 0;

        string fen = board.GetFenString();
        WriteLine("fen: " + fen);

        Move move = base.Think(board, timer);
        this.board = Board.CreateBoardFromFEN(fen);
        List<(Move, string)> list = BestLine(move);

        int mateIn = list.Last().Item2.Contains('#') ? (list.Count + 1) / 2 : -1;
        string score = mateIn == -1 ? Num(bestScore / 100.0, 2) : "#" + mateIn;

        WriteLine(
            $"best: {PrettyMove(move)} ({score})",
            mateIn >= 0
                ? ConsoleColor.Red
                : bestScore > 0
                    ? ConsoleColor.Green
                    : ConsoleColor.Yellow
        );

        double mem = GC.GetTotalMemory(false) / 1000000.0;
        WriteLine($"mem: {Num(mem)} MB");

        WriteLine(
            $"nodes: {Num(statsNodes + statsQNodes)}, search: {Num(statsNodes)}, qsearch: {Num(statsQNodes)}"
        );

        // evals per second
        int millis = timer.MillisecondsElapsedThisTurn;
        WriteLine(
            $"speed: {Num(millis == 0 ? double.PositiveInfinity : (statsNodes + statsQNodes) / millis)}k nodes/s"
        );

        WriteLine("best line: " + string.Join(" ", list.Select(x => x.Item2)));

        // print search stats
        /* Write("search stats:"); */
        /* for (int i = 0; i < statsSearch.Length; i++) */
        /* { */
        /*     if (statsSearch[i] == 0) */
        /*         continue; */
        /*     Write($"{i - 16}: {statsSearch[i]}"); */
        /* } */
        return move;
    }

    public List<(Move, string)> BestLine(Move move, List<(Move, string)>? line = null)
    {
        line ??= new List<(Move, string)>();
        line.Add((move, PrettyMove(move)));
        board.MakeMove(move);
        Transposition trans = tt.GetValueOrDefault(board.ZobristKey);
        if (trans.Depth != 0 && !trans.BestMove.IsNull && line.Count <= 16)
        {
            BestLine(trans.BestMove, line);
        }
        board.UndoMove(move);

        return line;
    }

    public static void WriteLine(string s, ConsoleColor color = ConsoleColor.White)
    {
        Write("[Zypher] ", ConsoleColor.Blue);
        Write(s, color);
        Console.WriteLine();
    }

    public static void Write(string s, ConsoleColor color = ConsoleColor.White)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.ForegroundColor = color;
        Console.Write(s);
        Console.ResetColor();
    }

    public string PrettyMove(Move move)
    {
        string[] icons = new string[] { "󰡙", "󰡘", "󰡜", "󰡛", "󰡚", "󰡗 " };
        string[] files = new string[] { "a", "b", "c", "d", "e", "f", "g", "h" };
        int idx = (int)move.MovePieceType - 1;
        string ret = icons[idx];
        if (move.IsCapture)
            ret += "x";
        ret += files[move.TargetSquare.File] + "" + (move.TargetSquare.Rank + 1);
        if (move.IsPromotion)
        {
            ret += "=";
            ret += icons[idx];
        }
        if (move.IsCastles)
        {
            ret = move.TargetSquare.File == 6 ? "O-O" : "O-O-O";
        }
        board.MakeMove(move);
        if (board.IsInCheckmate())
            ret += "#";
        else if (board.IsInCheck())
            ret += "+";
        board.UndoMove(move);
        return ret;
    }

    public static string Num(double value, int decimals = 0)
    {
        return value == double.PositiveInfinity
            ? "∞"
            : value == double.NegativeInfinity
                ? "-∞"
                : value.ToString("N" + decimals, CultureInfo.InvariantCulture);
    }

    public override double Search(double alpha, double beta, int depth, int ply)
    {
        int idx = searchDepth + 16;

        statsSearch[idx]++;

        int nodesBefore = statsNodes + statsQNodes;

        if (depth <= 0)
            statsQNodes++;
        else
            statsNodes++;
        double ret = base.Search(alpha, beta, depth, ply);
        if (ply == 0)
        {
            bestScore = ret;
            double branchingFactor = (statsNodes + statsQNodes) / (double)nodesBefore;

            WriteLine(
                $"{searchDepth}: {PrettyMove(thinkBestMove), -5} {Num(statsNodes + statsQNodes), -10} $ {Num(ret / 100.0, 2), -6} {Num(branchingFactor, 2)}"
            );
        }
        return ret;
    }
}
