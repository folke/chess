using System;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    public static int offset = 200 + 165;
    public static string mg_pesto_packed = "ŭŭŭŭŭŭŭŭǏǳƪǌƱǫƏŢŧŴƇƌƮƥƆřşźųƂƄŹžŖŒūŨŹžųŷŔœũũţŰŰƎšŊŬřŖŞƅƓŗŭŭŭŭŭŭŭŭÆĔŋļƪČŞĂĤńƵƑƄƫŴŜľƩƒƮǁǮƶƙŤžƀƢƒƲſƃŠűŽźƉƀƂťŖŤŹŷƀžƆŝŐĸšŪŬſşŚĄŘĳŌŜőŚŖŐűěňŔŃŴťœŽśŠƋƨſľŝƒƘƕƐƟƒūũŲƀƟƒƒŴūŧźźƇƏŹŷűŭżżżŻƈſŷűżŽŭŴƂƎŮŌŪşŘŠšņŘƍƗƍƠƬŶƌƘƈƍƧƫƽưƇƙŨƀƇƑžƚƪŽŕŢŴƇƅƐťřŉœšŬŶŦųŖŀŔŝŜŰŭŨŌŁŝřŤŬŸŧĦŚŠŮžŽŴňœőŭƊŹƨƙƘƚŕņŨŮŝƦƉƣŠŜŴŵƊƥƜƦŒŒŝŝŬžūŮŤœŤţūũŰŪşůŢūŨůŻŲŊťŸůŵżŪŮŬśŤŷŞŔŎĻĬƄŽŞĵŋůźƊŬřŦťũŇŐŤƅůŝřųƃŗŜřšŒŏŔşŉļŬŒņĿŁŌĺşşŗĿŁŏŞŒŮŴťĭłŝŶŵŞƑŹķŵőƅŻ";
    public static string eg_pesto_packed = "ŭŭŭŭŭŭŭŭȟȚȋǳȀǱȒȨǋǑǂưƥƢƿǁƍƅźŲūűžžźŶŪŦŦťŰŬűŴŧŮŭŨŬťźŵŵŷźŭůŦŭŭŭŭŭŭŭŭĳŇŠőŎŒĮĊŔťŔūŤŔŕĹŕřŷŶŬŤŚńŜŰƃƃƃŸŵśśŧŽƆŽžűśŖŪŬżŷŪřŗŃřţŨūřŖŁŐĺŖŞŗśĻĭşŘŢťŦŤŜŕťũŴšŪŠũşůťŭŬūųŭűŪŶŹŶŻŷŰůŧŰźƀŴŷŪŤšŪŵŷźŰŦŞşśŦŬűŤŞŒŖŤŖŨŤŝŨŜźŷſżŹŹŵŲŸźźŸŪŰŵŰŴŴŴŲűŪŨŪűŰźŮůŮŬůŰŲŵűŨŧťŢũŭŨŬŦšťŝŧŧŭůŤŤŢŪŤůŰŬŨŠűřŤƃƃƈƈƀŷƁŜƁƍƖƧƆƋŭřųŶƞƜƐƀŶŰƃƅƚƦƕƦƑśƉƀƜƌƏƔƄŝŒżųŶžŷŲŗŖŏŝŝŖŉōŌőŗłŨōřńģŊśśŢżűŜšžŻžžƓƄŸŷžƄżƁƚƙźťƃƅƈƇƎƇŰśũƂƅƈƄŶŢŚŪŸƂƄŽŴŤŒŢűźŻűŨŜĸŋŘŢőşŕł";
    int maxDepth = 3;
    int quiescenceDepth = 2;
    int evals;
    int[][] mg_pesto_table = Unpack(mg_pesto_packed);
    int[][] eg_pesto_table = Unpack(eg_pesto_packed);
    int[] mg_value = { 82, 337, 365, 477, 1025, 0 };
    int[] eg_value = { 94, 281, 297, 512, 936, 0 };
    int[] gamephaseInc = { 0, 1, 1, 2, 4, 0 };

    public MyBot()
    {
        new ChessTables().Generate();
    }


    public Move Think(Board board, Timer timer)
    {
        evals = 0;
        double current = EvaluateBoard(board);
        Console.WriteLine($"MyBot: {current}");

        Move bestMove = Move.NullMove;
        double bestScore = double.NegativeInfinity;

        Move[] moves = board.GetLegalMoves();
        foreach (Move move in moves)
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
        Console.WriteLine($"MyBot: {bestMove} ({bestScore}) in {evals} evals");

        return bestMove;
    }

    private double Quiescence(double alpha, double beta, int depth, Board board)
    {
        double standPat = EvaluateBoard(board);
        if (depth == 0)
            return standPat;
        if (standPat >= beta)
            return beta;
        if (alpha < standPat)
            alpha = standPat;

        Move[] moves = board.GetLegalMoves(true); // Assuming this method returns only capturing moves.
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            double score = -Quiescence(-beta, -alpha, depth - 1, board);
            board.UndoMove(move);

            if (score >= beta)
                return beta;
            if (score > alpha)
                alpha = score;
        }
        return alpha;
    }


    private double AlphaBeta(double alpha, double beta, int depth, Board board)
    {
        if (depth == 0)
            return Quiescence(alpha, beta, quiescenceDepth, board);

        Move[] moves = board.GetLegalMoves();
        foreach (Move move in moves)
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
        evals++;
        if (board.IsDraw())
            return 0;
        if (board.IsInCheckmate())
            return board.IsWhiteToMove ? double.NegativeInfinity : double.PositiveInfinity;

        int[] mg = { 0, 0 };
        int[] eg = { 0, 0 };
        int gamePhase = 0;

        PieceList[] pieceLists = board.GetAllPieceLists();
        foreach (PieceList pieceList in pieceLists)
        {
            for (int i = 0; i < pieceList.Count; i++)
            {
                Piece piece = pieceList.GetPiece(i);
                int p = (int)piece.PieceType - 1;
                int c = piece.IsWhite ? 0 : 1;
                int sq = piece.IsWhite ? piece.Square.Index : piece.Square.Index ^ 56;
                mg[c] += mg_value[p] + mg_pesto_table[p][sq];
                eg[c] += eg_value[p] + eg_pesto_table[p][sq];
                gamePhase += gamephaseInc[p];
            }
        }

        /* tapered eval */
        int Side2Move = board.IsWhiteToMove ? 0 : 1;
        int other = Side2Move ^ 1;
        int mgScore = mg[Side2Move] - mg[other];
        int egScore = eg[Side2Move] - eg[other];
        int mgPhase = gamePhase;
        if (mgPhase > 24) mgPhase = 24; /* in case of early promotion */
        int egPhase = 24 - mgPhase;
        return (mgScore * mgPhase + egScore * egPhase) / 24;
    }

    public static int[][] Unpack(string packedString)
    {
        int[][] unpacked = new int[6][];
        for (int i = 0; i < 6; i++)
        {
            unpacked[i] = new int[64];
            for (int j = 0; j < 64; j++)
                unpacked[i][j] = (short)packedString[i * 64 + j] - offset;
        }
        return unpacked;
    }

}
