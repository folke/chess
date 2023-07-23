using ChessChallenge.API;
using System;

public class MyBot : IChessBot
{
    private int maxDepth = 3;

    public Move Think(Board board, Timer timer)
    {
        double current = EvaluateBoard(board);
        Console.WriteLine($"MyBot: {current}");

        Move bestMove = Move.NullMove;
        double bestScore = double.NegativeInfinity;

        Move[] moves = board.GetLegalMoves();
        foreach (var move in moves)
        {
            board.MakeMove(move);
            double score = -AlphaBeta(double.NegativeInfinity, double.PositiveInfinity, maxDepth - 1, board);
            board.UndoMove(move);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }
        Console.WriteLine($"MyBot: {bestMove} ({bestScore})");
        board.MakeMove(bestMove);
        if (board.IsInCheckmate())
        {
            Console.WriteLine("MyBot: Checkmate!");
        }
        else if (board.IsDraw())
        {
            Console.WriteLine("MyBot: Draw!");
        }
        else if (board.IsInCheck())
        {
            Console.WriteLine("MyBot: Check!");
        }

        return bestMove;
    }


    private double AlphaBeta(double alpha, double beta, int depth, Board board)
    {
        if (depth == 0 || board.IsInCheckmate() || board.IsDraw())
        {
            return EvaluateBoard(board);
        }

        Move[] moves = board.GetLegalMoves();
        foreach (var move in moves)
        {
            board.MakeMove(move);
            double score = -AlphaBeta(-beta, -alpha, depth - 1, board);
            board.UndoMove(move);

            if (score >= beta)
                return score; // Beta cut-off
            if (score > alpha)
                alpha = score; // Alpha gets updated
        }

        return alpha;
    }


    private double EvaluateBoard(Board board)
    {
        if (board.IsDraw())
        {
            return 0;
        }

        if (board.IsInCheckmate())
        {
            return board.IsWhiteToMove ? double.NegativeInfinity : double.PositiveInfinity;
        }
        double score = 0.0;
        int whitePieceCount = 0;
        int blackPieceCount = 0;

        PieceList[] pieceLists = board.GetAllPieceLists();
        foreach (var pieceList in pieceLists)
        {
            for (int i = 0; i < pieceList.Count; i++)
            {
                Piece piece = pieceList.GetPiece(i);
                if (piece.IsWhite)
                {
                    whitePieceCount++;
                }
                else
                {
                    blackPieceCount++;
                }

                double pieceValue = PieceValues[(int)piece.PieceType - 1];
                double positionValue = GetPositionValue(piece);
                score += (pieceValue + positionValue) * (piece.IsWhite ? 1 : -1);
            }
        }
        int totalPieceCount = whitePieceCount + blackPieceCount;
        return board.IsWhiteToMove ? score : -score;
    }


    private double GetPositionValue(Piece piece)
    {
        int index = piece.IsWhite ? piece.Square.Index : 63 - piece.Square.Index;
        return PieceSquareTables[(int)piece.PieceType - 1][index] / 50.0;
    }
    private static readonly double[] PieceValues = { 1.0, 3.0, 3.0, 5.0, 9.0, 100.0 };

    private static readonly double[][] PieceSquareTables = new double[][]
    {
        // Pawn
        new double[]
        {
            0, 0, 0, 0, 0, 0, 0, 0,
            5, 10, 10, -20, -20, 10, 10, 5,
            5, -5, -10, 0, 0, -10, -5, 5,
            0, 0, 0, 20, 20, 0, 0, 0,
            5, 5, 10, 25, 25, 10, 5, 5,
            10, 10, 20, 30, 30, 20, 10, 10,
            50, 50, 50, 50, 50, 50, 50, 50,
            0, 0, 0, 0, 0, 0, 0, 0
        },
        // Knight
        new double[]
        {
            -50, -40, -30, -30, -30, -30, -40, -50,
            -40, -20, 0, 0, 0, 0, -20, -40,
            -30, 0, 10, 15, 15, 10, 0, -30,
            -30, 5, 15, 20, 20, 15, 5, -30,
            -30, 0, 15, 20, 20, 15, 0, -30,
            -30, 5, 10, 15, 15, 10, 5, -30,
            -40, -20, 0, 5, 5, 0, -20, -40,
            -50, -40, -30, -30, -30, -30, -40, -50
        },
        // Bishop
        new double[]
        {
            -20, -10, -10, -10, -10, -10, -10, -20,
            -10, 0, 0, 0, 0, 0, 0, -10,
            -10, 0, 5, 10, 10, 5, 0, -10,
            -10, 5, 5, 10, 10, 5, 5, -10,
            -10, 0, 10, 10, 10, 10, 0, -10,
            -10, 10, 10, 10, 10, 10, 10, -10,
            -10, 5, 0, 0, 0, 0, 5, -10,
            -20, -10, -10, -10, -10, -10, -10, -20
        },
        // Rook
        new double[]
        {
            0, 0, 0, 0, 0, 0, 0, 0,
            5, 10, 10, 10, 10, 10, 10, 5,
            -5, 0, 0, 0, 0, 0, 0, -5,
            -5, 0, 0, 0, 0, 0, 0, -5,
            -5, 0, 0, 0, 0, 0, 0, -5,
            -5, 0, 0, 0, 0, 0, 0, -5,
            -5, 0, 0, 0, 0, 0, 0, -5,
            0, 0, 0, 5, 5, 0, 0, 0
        },
        // Queen
        new double[]
        {
            -20, -10, -10, -5, -5, -10, -10, -20,
            -10, 0, 0, 0, 0, 0, 0, -10,
            -10, 0, 5, 5, 5, 5, 0, -10,
            -5, 0, 5, 5, 5, 5, 0, -5,
            0, 0, 5, 5, 5, 5, 0, -5,
            -10, 5, 5, 5, 5, 5, 0, -10,
            -10, 0, 5, 0, 0, 0, 0, -10,
            -20, -10, -10, -5, -5, -10, -10, -20
        },
        // King
        new double[]
        {
            -30, -40, -40, -50, -50, -40, -40, -30,
            -30, -40, -40, -50, -50, -40, -40, -30,
            -30, -40, -40, -50, -50, -40, -40, -30,
            -30, -40, -40, -50, -50, -40, -40, -30,
            -20, -30, -30, -40, -40, -30, -30, -20,
            -10, -20, -20, -20, -20, -20, -20, -10,
            20, 20, 0, 0, 0, 0, 20, 20,
            20, 30, 10, 0, 0, 10, 30, 20
        }
    };
}
