﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ChessChallenge.API;

public class MyBot7 : IChessBot
{
    public class Transposition
    {
        public Move? BestMove;
        public int Depth;
        public double Score;
        public int Type; // 0 = exact, 1 = lower bound, 2 = upper bound
    }

    public int maxDepth = 9,
        searchDepth;
    public readonly int[] pesto,
        pieceValues =  { 82, 337, 365, 477, 1025, 0, 94, 281, 297, 512, 936, 0 },
        gamephaseInc =  { 0, 1, 1, 2, 4, 0 };
    public Timer timer;
    public Board board;
    public readonly Dictionary<ulong, Transposition> transpositionTable = new();
    public Move[] killerMoves = new Move[1000];
    public Move thinkBestMove;
    public double thinkBestScore,
        iterationBestScore,
        timeLimit;

    public Move Think(Board b, Timer t)
    {
        board = b;
        timer = t;
        int pieces = BitOperations.PopCount(board.AllPiecesBitboard);
        timeLimit =
            timer.MillisecondsRemaining
            * (
                pieces > 26
                    ? 0.01
                    : pieces > 12
                        ? 0.05
                        : 0.1
            ); // calculate time limit for this move
#if DEBUG
        timeLimit = 100;
#endif
        thinkBestMove = Move.NullMove;

        // e2e4 for first move for white
        if (board.ZobristKey == 13227872743731781434)
            return new Move("e2e4", board);

        try
        {
            for (searchDepth = 1; searchDepth <= maxDepth; searchDepth++)
            {
                if (
                    (thinkBestScore = Search(iterationBestScore = -32001, 32001, searchDepth, true))
                    > 9000
                )
                    break;
            }
        }
        catch (TimeoutException) { }

        return thinkBestMove;
    }

    public virtual double Quiescence(double alpha, double beta, int depthRemaining = 3)
    {
        if (timer.MillisecondsElapsedThisTurn >= timeLimit)
            throw new TimeoutException();
        double standPat = Evaluate();
        if (depthRemaining == 0 || standPat >= beta)
            return standPat;
        alpha = Math.Max(alpha, standPat);
        foreach (Move move in GetMoves(true, false))
        {
            board.MakeMove(move);
            alpha = Math.Max(alpha, -Quiescence(-beta, -alpha, depthRemaining - 1));
            board.UndoMove(move);
            if (alpha >= beta)
                return beta;
        }
        return alpha;
    }

    public virtual double Search(double alpha, double beta, int depthRemaining, bool root)
    {
        if (timer.MillisecondsElapsedThisTurn >= timeLimit)
            throw new TimeoutException();
        double alphaOrig = alpha;

        Move? bestMove = null;
        // Check transposition table
        if (
            transpositionTable.TryGetValue(board.ZobristKey, out var trans)
            && !root
            && trans.Depth >= depthRemaining
        /* && ( */
        /*     (trans.Type == 0) */
        /*     || (trans.Type == 1 && trans.Score <= alpha) */
        /*     || (trans.Type == 2 && trans.Score >= beta) */
        /* ) */
        )
        {
            if (trans.Type == 1) // Fail-low
                alpha = Math.Max(alpha, trans.Score);
            else if (trans.Type == 2) // Fail-high
                beta = Math.Min(beta, trans.Score);
            if (trans.Type == 0 || alpha >= beta)
                return trans.Score;
            bestMove = trans.BestMove;
            /* return trans.Score; */
        }

        if (depthRemaining == 0)
            return Quiescence(alpha, beta);

        double bestScore = -32002;
        Move[] moves = GetMoves(false, root);

        if (moves.Length == 0)
            bestScore = board.IsInCheck() ? -32000 + board.PlyCount : 0;
        else
        {
            foreach (Move move in moves)
            {
                board.MakeMove(move);
                double score = -Search(
                    -beta,
                    -alpha,
                    // Check extension, but only if we're not in quiescence search
                    board.IsInCheck()
                        ? depthRemaining
                        : depthRemaining - 1,
                    false
                );
                /* if (root && board.IsRepeatedPosition()) */
                /*     score -= 50; */
                board.UndoMove(move);
                if (root && score > iterationBestScore)
                {
                    iterationBestScore = score;
                    thinkBestMove = move;
                }

                if (score >= beta)
                {
                    bestScore = score;
                    bestMove = move;
                    break;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;

                    // Update killer moves
                    int idx = 2 * board.PlyCount;
                    if (!move.IsCapture && !move.IsPromotion && killerMoves[idx] != move)
                    {
                        killerMoves[idx + 1] = killerMoves[idx];
                        killerMoves[idx] = move;
                    }
                    alpha = Math.Max(alpha, score);
                }
            }
        }

        if (trans == null || depthRemaining >= trans.Depth)
            transpositionTable[board.ZobristKey] = new Transposition
            {
                BestMove = bestMove,
                Depth = depthRemaining,
                Score = bestScore,
                Type =
                    bestScore <= alphaOrig
                        ? 2
                        : bestScore >= beta
                            ? 1
                            : 0
            };
        return bestScore;
    }

    private double Evaluate()
    {
        int[] mg =  { 0, 0 },
            eg =  { 0, 0 };
        int gamePhase = 0;

        foreach (Piece piece in board.GetAllPieceLists().SelectMany(x => x))
        {
            int p = (int)piece.PieceType - 1,
                side = Convert.ToInt32(piece.IsWhite),
                sq = piece.Square.Index ^ (side * 56) + p * 128;
            mg[side] += pieceValues[p] + pesto[sq];
            eg[side] += pieceValues[p + 6] + pesto[sq + 64];
            gamePhase += gamephaseInc[p];
        }
        // FIXME:
        /* score_mg[turn] += 14; */
        /* tapered eval */
        int turn = Convert.ToInt32(board.IsWhiteToMove);
        double factor = Math.Min(1, gamePhase / 24.0);
        return (mg[turn] - mg[turn ^ 1]) * factor + (eg[turn] - eg[turn ^ 1]) * (1 - factor);
    }

    private Move[] GetMoves(bool capturesOnly, bool root)
    {
        // Check if the position exists in the transposition table
        var bestMove = transpositionTable.GetValueOrDefault(board.ZobristKey)?.BestMove;
        // Order the moves based on whether they match the best move from the transposition table, and then by your existing criteria
        return board
            .GetLegalMoves(capturesOnly)
            .OrderByDescending(
                m =>
                    (root && m == thinkBestMove ? 100000 : 0)
                    + (m == bestMove ? 30000 : 0)
                    + (
                        m == killerMoves[2 * board.PlyCount]
                            ? 20000
                            : m == killerMoves[2 * board.PlyCount + 1]
                                ? 10000
                                : 0
                    )
                    + (
                        m.IsCapture
                            ? (
                                pieceValues[(int)m.CapturePieceType - 1]
                                - pieceValues[(int)m.MovePieceType - 1]
                            ) * 10
                            : 0
                    )
            )
            .ToArray();
    }

    public MyBot7()
    {
        pesto = pesto_packed
            .SelectMany(x => decimal.GetBits(x).Take(3))
            .SelectMany(BitConverter.GetBytes)
            .Select((x, i) => (i < 128 ? 64 : 0) + (sbyte)x)
            .ToArray();
        pesto[128] = -167;
        pesto[149] = 129;
    }

    public static decimal[] pesto_packed =
    {
        9900224743556610877869310144M,
        53494929773592646717406330372M,
        63361911437403633960110968242M,
        55980234549002264723438880465M,
        59654145891518603609475170205M,
        59654145893093148305727209664M,
        954013039163236101123894642M,
        64935961651925880168888071672M,
        59956410478277939986375231949M,
        57490168916513471633213602752M,
        64332568855825051878625951936M,
        73975673041650402209951751997M,
        16425759950871185596799597777M,
        76777760120318750607755330853M,
        78595648229894077393035655145M,
        72397603832076830576582202111M,
        78889629778405879187202366150M,
        66826963659930458069259577335M,
        7757653057745239109419795439M,
        72705993735964324534956593424M,
        74868539370796317355223215318M,
        76761759779885689695443545834M,
        12431563372940844378304155878M,
        78617830389056581075051295267M,
        4660479870402696979694816762M,
        349478860696496719657048846M,
        77049583498729585393217502687M,
        75201173902974282571807979513M,
        2799919366840315712364410882M,
        76748703012399528202368911886M,
        79220828438297994789472763380M,
        74271495670478730626235234052M,
        19258339923357797006828382752M,
        5025717454825805570608087888M,
        79214745624079154851546133992M,
        69318597643513028336415078665M,
        5263606013117200425645043924M,
        1557153346508307212252415760M,
        1555920715755045318604688651M,
        927250862980111682775022852M,
        79222122551469963312005383427M,
        78597065291617332687567123705M,
        3748883189489600233040184055M,
        16746314055552891992505265211M,
        74567630204179871405525430259M,
        78304529001881770548856230399M,
        633443427640874921396339442M,
        64027015645723603412304334600M,
        12727669886448294598458873591M,
        2808500832322018468106737978M,
        14568901671372667762396108291M,
        1559594753990597590947144223M,
        66204276563121428362013370858M,
        4026775051395136830416150779M,
        74278938118322134367848955677M,
        68380357261234488370305304300M,
        65275887995908017650584125391M,
        2487897721964540181596988116M,
        73946203683864356149181293809M,
        3432320166635870018569768949M,
        8385217952651404553569636618M,
        75834816852832840822747177242M,
        4029302052848854096214228461M,
        66201924918364098993134961678M
    };
}
