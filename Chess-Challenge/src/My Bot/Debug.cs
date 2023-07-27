using System;
using System.Collections.Generic;
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

    public void Init()
    {
        didInit = true;
        Console.WriteLine("DebugBot");
        new ChessTables().Generate();
    }

    public static double TimeLimit(double timeLimit)
    {
        /* return 100; */
        return Enabled ? 10000 : 100;
    }

    public new Move Think(Board board, Timer timer)
    {
        if (!didInit)
            Init();

        bestScore = 0;
        statsNodes = 0;
        statsQNodes = 0;
        statsEvals = 0;

        string fen = board.GetFenString();
        Write("fen: " + fen);

        Move move = base.Think(board, timer);
        this.board = Board.CreateBoardFromFEN(fen);
        Write($"best move: {PrettyMove(move)}");
        Write($"score: {Math.Round(iterationBestScore / 100.0, 2)}");

        double mem = GC.GetTotalMemory(false) / 1000000.0;
        Write($"mem: {mem} MB");

        Write($"nodes: {statsNodes}");
        Write($"qnodes: {statsQNodes}");
        Write($"evals: {statsEvals}");

        // evals per second
        int millis = timer.MillisecondsElapsedThisTurn;
        Write(
            $"total: {(millis == 0 ? double.PositiveInfinity : (statsNodes + statsQNodes) / millis)}n/s"
        );

        List<(Move, string)> list = BestLine(move);
        Write("best line: " + string.Join(" ", list.Select(x => x.Item2)));
        if (list.Last().Item2.Contains('#'))
        {
            int mateIn = (list.Count - 1) / 2;
            if (mateIn == 0)
                Write("checkmate!");
            else
                Write("mate in " + (list.Count - 1) / 2);
        }
        return move;
    }

    public List<(Move, string)> BestLine(Move move, List<(Move, string)>? line = null)
    {
        line ??= new List<(Move, string)>();
        line.Add((move, PrettyMove(move)));
        board.MakeMove(move);
        Transposition trans = transpositionTable.GetValueOrDefault(board.ZobristKey);
        if (trans.Depth != 0 && !trans.BestMove.IsNull && line.Count <= 16)
        {
            BestLine(trans.BestMove, line);
        }
        board.UndoMove(move);

        return line;
    }

    public static void Write(string s)
    {
        Console.WriteLine("[MyBot] " + s);
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

    public override double Search(double alpha, double beta, int ply, bool root)
    {
        if (ply <= 0)
            statsQNodes++;
        else
            statsNodes++;
        double ret = base.Search(alpha, beta, ply, root);
        if (root)
        {
            bestScore = iterationBestScore;
            Write(
                $"{thinkBestMove} ({Math.Round(iterationBestScore / 100.0, 2)}) {statsNodes + statsQNodes}x {searchDepth}"
            );
        }
        return ret;
    }
}
