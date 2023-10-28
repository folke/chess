using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ChessChallenge.API;

public class DebugBot : MyBot, IChessBot
{
    public const int GameDuration = 60 * 1000;
    public const bool Enabled = true;

    bool didInit = false;
    int statsNodes = 0;
    int statsQNodes = 0;
    double bestScore = 0;
    Board rootBoard;

    public void Init()
    {
        didInit = true;
        Console.WriteLine("DebugBot");
        /* new ChessTables().Generate(); */
    }

    public static double TimeLimit(double timeLimit)
    {
        return timeLimit;
        /* return 500; */
        /* return Enabled ? 10000 : 100; */
    }

    public new Move Think(Board board, Timer timer)
    {
        if (!didInit)
            Init();

        string fen = board.GetFenString();
        WriteLine("fen: " + fen);
        rootBoard = Board.CreateBoardFromFEN(fen);
        bestScore = 0;
        statsNodes = 0;
        statsQNodes = 0;

        Move move = base.Think(board, timer);
        List<(Move, string)> list = BestLine(move);

        int mateIn = list.Count > 0 && list.Last().Item2.Contains('#') ? (list.Count + 1) / 2 : -1;
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
        if (move.IsNull || !rootBoard.GetLegalMoves().Contains(move))
            return line;
        line.Add((move, PrettyMove(move)));
        rootBoard.MakeMove(move);
        Transposition trans = tt[rootBoard.ZobristKey & 0x7FFFFF];
        if (trans.ZobristKey == rootBoard.ZobristKey && !trans.BestMove.IsNull && line.Count <= 32)
        {
            BestLine(trans.BestMove, line);
        }
        rootBoard.UndoMove(move);

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
        if (move.IsNull)
            return "NullMove ???";

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
            ret += icons[(int)move.PromotionPieceType - 1];
        }
        if (move.IsCastles)
        {
            ret = move.TargetSquare.File == 6 ? "O-O" : "O-O-O";
        }
        if (!rootBoard.GetLegalMoves().Contains(move))
            return ret + " ???";
        rootBoard.MakeMove(move);
        if (rootBoard.IsInCheckmate())
            ret += "#";
        else if (rootBoard.IsInCheck())
            ret += "+";
        rootBoard.UndoMove(move);
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
                $"{searchDepth}: {PrettyMove(thinkBestMove), -5} {Num(statsNodes + statsQNodes - nodesBefore), -10} $ {Num(ret / 100.0, 2), -6} {Num(branchingFactor, 2)}"
            );
        }
        return ret;
    }
}
