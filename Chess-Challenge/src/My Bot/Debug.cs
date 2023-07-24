using System;
using System.Collections.Generic;
using System.Linq;
using ChessChallenge.API;

public class DebugBot : MyBot, IChessBot
{
    private readonly bool debug = true;
    protected int evals;

    public DebugBot()
    {
        Console.WriteLine("DebugBot");
        new ChessTables().Generate();
    }

    public new Move Think(Board board, Timer timer)
    {
        if (!debug)
            return base.Think(board, timer);
        evals = 0;
        string fen = board.GetFenString();
        Write("fen: " + fen);

        OnDepth = () =>
            Write($"{thinkMoves.Last()} ({thinkScores.Last() / 100.0}) {evals}x {thinkDepth}");

        Move move = base.Think(board, timer);
        this.board = Board.CreateBoardFromFEN(fen);
        Write($"best move: {PrettyMove(move)}");
        double score = thinkScores[thinkMoves.ToList().IndexOf(move)];
        Write($"score: {score / 100.0}");

        double mem = GC.GetTotalMemory(false) / 1000000.0;
        Write($"mem: {mem} MB");

        // evals per second
        int millis = timer.MillisecondsElapsedThisTurn;
        Write($"evals: {(millis == 0 ? double.PositiveInfinity : evals / millis)}k/s");
        List<(Move, string)> list = BestLine(move);
        Write("best line: " + string.Join(" ", list.Select(x => x.Item2)));
        if (list.Last().Item2.Contains('#'))
            Write("mate in " + (list.Count - 1) / 2);
        return move;
    }

    public List<(Move, string)> BestLine(Move move, List<(Move, string)>? line = null)
    {
        line ??= new List<(Move, string)>();
        line.Add((move, PrettyMove(move)));
        board.MakeMove(move);
        Transposition? trans = transpositionTable.GetValueOrDefault(board.ZobristKey);
        if (trans != null && trans.BestMove != null && line.Count < thinkDepth)
        {
            BestLine((Move)trans.BestMove, line);
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

    protected override double AlphaBeta(double alpha, double beta, int depth, bool quiescence)
    {
        evals++;
        return base.AlphaBeta(alpha, beta, depth, quiescence);
    }
}
