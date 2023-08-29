﻿using System;
using System.Linq;
using System.Collections.Generic;
using ChessChallenge.API;

// TODO:
// - [ ] experiment with maxDepth > 9
// - [ ] determine correct 32000 values (and format them with _)
// - [ ] get oproper game phase from evaluate for time management and skipping NMP

public class MyBot : IChessBot
{
    public struct Transposition
    {
        public Move BestMove;
        public double Score;
        public int Depth,
            Flag; // 0 = exact, 1 = lower bound, 2 = upper bound
    }

    public int searchDepth,
        phase,
        gamePhase;
    public readonly int[] pesto,
        pieceValues =  { 82, 337, 365, 477, 1025, 0, 94, 281, 297, 512, 936, 0 },
        gamephaseInc =  { 0, 1, 1, 2, 4, 0 };
    public Timer timer;
    public Board board;
    public readonly Dictionary<ulong, Transposition> tt = new();
    public Move[,] killerMoves = new Move[500, 2];
    public Move thinkBestMove;
    public double timeLimit;
    public int[,,] historyTable = new int[2, 64, 64];

    public Move Think(Board b, Timer t)
    {
        board = b;
        timer = t;
        Evaluate();
        gamePhase = phase;

        timeLimit = timer.MillisecondsRemaining / (gamePhase == 24 ? 60.0 : 30.0);
        timeLimit = DebugBot.TimeLimit(timeLimit); // #DEBUG

        // e2e4 for first move for white
        if (board.ZobristKey == 13227872743731781434)
            return new Move("e2e4", board);

        try
        {
            for (searchDepth = 1; searchDepth <= 50; searchDepth++)
                if (Search(-32001, 32001, searchDepth, 0) > 9000)
                    break;
        }
        catch (TimeoutException) { }
        return thinkBestMove;
    }

    public virtual double Search(double alpha, double beta, int depth, int ply)
    {
        if (timer.MillisecondsElapsedThisTurn >= timeLimit)
            throw new TimeoutException();

        double alphaOrig = alpha,
            bestScore = -32000,
            score;
        // Quiescence search (negative depth)
        bool quiescence = depth <= 0,
            root = ply == 0;
        int m = 0,
            nextPly = ply + 1;

        // Check transposition table
        Transposition trans = tt.GetValueOrDefault(board.ZobristKey);
        Move bestMove = trans.BestMove;
        if (!root && trans.Depth >= depth && trans.Depth > 0)
        {
            if (trans.Flag == 1) // lower bound
                alpha = Math.Max(alpha, trans.Score);
            else if (trans.Flag == 2) // upper bound
                beta = Math.Min(beta, trans.Score);
            if (trans.Flag == 0 || alpha >= beta) // exact
                return trans.Score;
        }

        // Get legal moves
        Span<Move> moves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref moves, quiescence);

        // Early exit if we're in checkmate or draw
        if (moves.Length == 0)
            if (board.IsInCheckmate())
                return bestScore + ply;
            else if (!quiescence)
                return 0;

        // Quiet search
        if (quiescence && (bestScore = alpha = Math.Max(alpha, Evaluate())) >= beta)
            return alpha;

        // Null move pruning
        if (depth > 2 && !root && gamePhase >= 12 && board.TrySkipTurn())
        {
            score = -Search(-beta, -beta + 1, depth - 3, nextPly);
            board.UndoSkipTurn();
            if (score >= beta)
                return score; // or return score if you are using fail-soft
        }

        // Move ordering
        Span<int> scores = stackalloc int[moves.Length];
        foreach (Move move in moves)
        {
            scores[m++] =
                move == bestMove
                    ? -80000
                    : move.IsCapture
                        ? -70000
                            - pieceValues[(int)move.CapturePieceType - 1]
                            + pieceValues[(int)move.MovePieceType - 1]
                        : killerMoves[ply, 0] == move || killerMoves[ply, 1] == move
                            ? -60000
                            : -historyTable[
                                Convert.ToInt32(board.IsWhiteToMove),
                                move.StartSquare.Index,
                                move.TargetSquare.Index
                            ];
        }
        scores.Sort(moves);

        m = 0;
        foreach (Move move in moves)
        {
            board.MakeMove(move);

            // Calculate check extension and late move reduction
            int delta = board.IsInCheck()
                ? 0
                : m >= 3 && depth >= 3 && !move.IsCapture
                    ? -2
                    : -1;
            m++;

            score = -Search(-beta, -alpha, depth + delta, nextPly);
            // do full depth search on fail high when doing LMR
            if (delta == -2 && score > alpha)
                score = -Search(-beta, -alpha, depth - 1, nextPly);

            // Avoid 3-fold repetition
            if (root && board.IsRepeatedPosition())
                score -= 50;

            board.UndoMove(move);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;

                if (root)
                    thinkBestMove = move;

                if (score >= beta)
                {
                    // Update killer moves
                    if (
                        !move.IsCapture
                        && !move.IsPromotion
                        && killerMoves[ply, 0] != move
                        && killerMoves[ply, 1] != move
                    )
                    {
                        killerMoves[ply, 1] = killerMoves[ply, 0];
                        killerMoves[ply, 0] = move;
                    }
                    if (!quiescence)
                        historyTable[
                            Convert.ToInt32(board.IsWhiteToMove),
                            bestMove.StartSquare.Index,
                            bestMove.TargetSquare.Index
                        ] += 1 << depth;
                    break;
                }
                alpha = Math.Max(alpha, score);
            }
        }

        // Update transposition table
        if (!quiescence)
            tt[board.ZobristKey] = new Transposition
            {
                BestMove = bestMove,
                Depth = depth,
                Score = bestScore,
                Flag =
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
        int mg = 0,
            eg = 0,
            i = -1;
        phase = 0;

        while (++i < 12)
        {
            int p = i % 6;
            ulong bb = board.GetPieceBitboard((PieceType)(p + 1), i >= 6);
            while (bb > 0)
            {
                int sq = BitboardHelper.ClearAndGetIndexOfLSB(ref bb) ^ (i / 6 * 56) + p * 128;
                mg += pieceValues[p] + pesto[sq];
                eg += pieceValues[p + 6] + pesto[sq + 64];
                phase += gamephaseInc[p];
            }
            if (i == 5)
            {
                mg = -mg;
                eg = -eg;
            }
        }
        return (mg * phase + eg * (24 - phase)) / 24.0 * (board.IsWhiteToMove ? 1 : -1);
    }

    public MyBot()
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
