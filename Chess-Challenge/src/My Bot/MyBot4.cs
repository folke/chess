using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ChessChallenge.API;

public class MyBot4 : IChessBot
{
    public class Transposition
    {
        public Move? BestMove;
        public int Depth;
        public double Score;
        public int Type; // 0 = exact, 1 = lower bound, 2 = upper bound
    }

    public int maxDepth = 9,
        quiescenceDepth = 3,
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
                    (
                        thinkBestScore = Search(
                            iterationBestScore = -32001,
                            32001,
                            searchDepth,
                            0,
                            false
                        )
                    ) > 9000
                )
                    break;
            }
        }
        catch (TimeoutException) { }

        return thinkBestMove;
    }

    public virtual double Search(
        double alpha,
        double beta,
        int depthRemaining,
        int depthFromRoot,
        bool quiescence
    )
    {
        if (timer.MillisecondsElapsedThisTurn >= timeLimit)
            throw new TimeoutException();

        // Check transposition table
        if (
            transpositionTable.TryGetValue(board.ZobristKey, out var trans)
            && trans.Depth >= depthRemaining
            && (
                (trans.Type == 0)
                || (trans.Type == 1 && trans.Score <= alpha)
                || (trans.Type == 2 && trans.Score >= beta)
            )
        )
        {
            return trans.Score;
        }

        double bestScore = -32002;
        Move? bestMove = null;

        if (board.IsDraw())
            bestScore = 0;
        else if (board.IsInCheckmate())
            bestScore = board.PlyCount - 32000;
        else
        {
            if (quiescence)
            {
                double standPat = Evaluate();
                if (depthRemaining == 0 || standPat >= beta)
                    return standPat;
                bestScore = alpha = Math.Max(alpha, standPat);
            }
            else if (depthRemaining == 0)
                return Search(alpha, beta, quiescenceDepth, depthFromRoot + 1, true);

            Move[] moves = GetMoves(quiescence, depthFromRoot == 0);

            foreach (Move move in moves)
            {
                board.MakeMove(move);
                double score = -Search(
                    -beta,
                    -alpha,
                    // TODO: extensions?
                    board.IsInCheck()
                        ? depthRemaining
                        : depthRemaining - 1,
                    depthFromRoot + 1,
                    quiescence
                );
                board.UndoMove(move);

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
                    if (!quiescence && !move.IsCapture)
                    {
                        // Shift killer moves
                        int idx = 2 * board.PlyCount;
                        killerMoves[idx + 1] = killerMoves[idx];
                        killerMoves[idx] = move;
                    }
                    alpha = Math.Max(alpha, score);
                }
            }
        }
        if (depthFromRoot == 0 && bestScore > iterationBestScore)
        {
            iterationBestScore = bestScore;
            thinkBestMove = (Move)bestMove;
        }

        if (!quiescence && (trans == null || depthRemaining >= trans.Depth))
            transpositionTable[board.ZobristKey] = new Transposition
            {
                BestMove = bestMove,
                Depth = depthRemaining,
                Score = bestScore,
                Type =
                    bestScore <= alpha
                        ? 1
                        : bestScore >= beta
                            ? 2
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
                c = 1,
                sq = piece.Square.Index;
            if (piece.IsWhite)
            {
                sq ^= 56;
                c = 0;
            }
            sq += p * 128;
            mg[c] += pieceValues[p] + pesto[sq];
            eg[c] += pieceValues[p + 6] + pesto[sq + 64];
            gamePhase += gamephaseInc[p];
        }

        /* tapered eval */
        int side = board.IsWhiteToMove ? 0 : 1;
        double factor = Math.Min(1, gamePhase / 24.0);
        return (mg[side] - mg[side ^ 1]) * factor + (eg[side] - eg[side ^ 1]) * (1 - factor);
    }

    private Move[] GetMoves(bool capturesOnly, bool root)
    {
        // Check if the position exists in the transposition table
        Move? bestMove = transpositionTable.GetValueOrDefault(board.ZobristKey)?.BestMove;

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

    public MyBot4()
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
