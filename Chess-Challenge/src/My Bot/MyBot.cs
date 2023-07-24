using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ChessChallenge.API;

public class Transposition
{
    public Move? BestMove;
    public int Depth;
    public double Score;
}

public class MyBot : IChessBot
{
    protected readonly int maxDepth = 9,
        quiescenceDepth = 3;
    protected int evals;
    protected readonly int[] pesto = Unpack(pesto_packed);
    protected readonly int[] mg_value =  { 82, 337, 365, 477, 1025, 0 },
        eg_value =  { 94, 281, 297, 512, 936, 0 },
        gamephaseInc =  { 0, 1, 1, 2, 4, 0 };
    protected Timer timer;
    protected Board board;
    protected double timeLimit;
    protected readonly Dictionary<ulong, Transposition> transpositionTable = new();

    protected double[] thinkScores;
    protected Move[] thinkMoves;
    protected int thinkDepth;
#if DEBUG
    protected Action OnDepth = () => { };
#endif

    public Move Think(Board board, Timer timer)
    {
        this.board = board;
        this.timer = timer;
        transpositionTable.Clear();
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

        thinkMoves = GetMoves(false).Reverse().ToArray();
        thinkScores = new double[thinkMoves.Length];
        evals = 0;
        try
        {
            for (thinkDepth = 1; thinkDepth <= maxDepth; thinkDepth++)
            {
                for (int i = thinkMoves.Length - 1; i >= 0; i--)
                {
                    Move move = thinkMoves[i];
                    board.MakeMove(move);
                    thinkScores[i] = -AlphaBeta(
                        double.NegativeInfinity,
                        double.PositiveInfinity,
                        thinkDepth - 1,
                        false
                    );
                    board.UndoMove(move);
                    if (thinkScores[i] > 9000)
                        return move;
                }
                Array.Sort(thinkScores, thinkMoves);
#if DEBUG
                OnDepth();
#endif
            }
        }
        catch (TimeoutException) { }

        return thinkMoves.Last();
    }

    protected double AlphaBeta(double alpha, double beta, int depth, bool quiescence)
    {
        evals++;
        if (timer.MillisecondsElapsedThisTurn >= timeLimit)
            throw new TimeoutException();

        // Check transposition table
        Transposition? trans = transpositionTable.GetValueOrDefault(board.ZobristKey);
        if (trans != null && trans.Depth >= depth && (trans.Score >= beta || trans.Score <= alpha))
            return trans.Score;

        double bestScore = -1000000 + board.PlyCount;
        if (board.IsDraw())
            return 0;
        if (board.IsInCheckmate())
            return bestScore;

        if (quiescence)
        {
            double standPat = EvaluateBoard(board);
            if (depth == 0 || standPat >= beta)
                return standPat;
            bestScore = alpha = Math.Max(alpha, standPat);
        }
        else if (depth == 0)
            return AlphaBeta(alpha, beta, quiescenceDepth - 1, true);

        Move[] moves = GetMoves(quiescence);

        Move? bestMove = null;
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            double score = -AlphaBeta(
                -beta,
                -alpha,
                quiescence && board.IsInCheck() ? depth : depth - 1,
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
                alpha = Math.Max(alpha, score);
            }
        }
        if (!quiescence && (trans == null || depth > trans.Depth))
            transpositionTable[board.ZobristKey] = new Transposition
            {
                BestMove = bestMove,
                Depth = depth,
                Score = bestScore
            };
        return bestScore;
    }

    private double EvaluateBoard(Board board)
    {
        /* if (board.IsDraw()) */
        /*     return 0; */
        /* if (board.IsInCheckmate()) */
        /*     return (board.IsWhiteToMove ? -1 : 1) * (1000000 - board.PlyCount); */
        int[] mg =  { 0, 0 },
            eg =  { 0, 0 };
        int gamePhase = 0;

        foreach (PieceList pieceList in board.GetAllPieceLists())
        {
            foreach (Piece piece in pieceList)
            {
                int p = (int)piece.PieceType - 1,
                    c = piece.IsWhite ? 0 : 1,
                    sq = piece.IsWhite ? piece.Square.Index ^ 56 : piece.Square.Index;
                mg[c] += mg_value[p] + pesto[p * 64 + sq];
                eg[c] += eg_value[p] + pesto[p * 64 + sq + 384];
                gamePhase += gamephaseInc[p];
            }
        }

        /* tapered eval */
        int Side2Move = board.IsWhiteToMove ? 0 : 1,
            other = Side2Move ^ 1,
            mgScore = mg[Side2Move] - mg[other],
            egScore = eg[Side2Move] - eg[other],
            mgPhase = Math.Min(gamePhase, 24), /* in case of early promotion */
            egPhase = 24 - mgPhase;
        return (mgScore * mgPhase + egScore * egPhase) / 24;
    }

    private Move[] GetMoves(bool capturesOnly)
    {
        // Check if the position exists in the transposition table
        Move? bestMove = transpositionTable.GetValueOrDefault(board.ZobristKey)?.BestMove;

        // Order the moves based on whether they match the best move from the transposition table, and then by your existing criteria
        return board
            .GetLegalMoves(capturesOnly)
            .OrderByDescending(m => bestMove != null && m.Equals(bestMove) ? 1 : 0)
            .ThenByDescending(
                m =>
                    m.IsCapture
                        ? mg_value[(int)m.CapturePieceType - 1] * 10
                            - mg_value[(int)m.MovePieceType - 1]
                        : 0
            )
            .ToArray();
    }

    public static int[] Unpack(decimal[] packed)
    {
        byte[] bytes = packed
            .SelectMany(decimal.GetBits)
            .Where((x, i) => i % 4 != 3)
            .SelectMany(BitConverter.GetBytes)
            .ToArray();

        List<int> unpacked = new();

        for (int i = 0; i < bytes.Length; i++)
        {
            sbyte val = (sbyte)bytes[i];
            unpacked.Add(val == 127 ? ((sbyte)bytes[++i] << 8) | (bytes[++i] & 0xFF) : val);
        }

        return unpacked.ToArray();
    }

    public static decimal[] pesto_packed =
    {
        41471592864384482877598859264M,
        17409887601821631051332869949M,
        78887137133169743586166762521M,
        933248175880678573666798843M,
        1105736530741228354794529M,
        64332568860346172495429107712M,
        73975673041650402209951751997M,
        5560058984440812850400017617M,
        5914126795823601470620906771M,
        63101016985140398505396402197M,
        70852572267311877511731740148M,
        5230984387153433381948353005M,
        15516752585356990262718428142M,
        4326736195471601203777699365M,
        8373091339930194166105119245M,
        78569303275198581863994362386M,
        2861768849049949437338840050M,
        6183863835919865627505535775M,
        10861112491793022221127066650M,
        71747221056685526318424321272M,
        3713781987343137926640037872M,
        276725271509073070601910778M,
        17930797124725275054260816925M,
        71149181307709662723740743196M,
        78298455908304418406708277488M,
        77019478912130287207015709955M,
        71782440188489937095718601227M,
        78953797796922928146874879713M,
        2143349959005656865694087660M,
        79169968550550688331249740310M,
        70200900524388083853142710757M,
        11432849438402096636388042225M,
        15500067785228M,
        39495608882012536669814587392M,
        39504071359795477585338533376M,
        7466724057192413994107714304M,
        77054485501710301184256771341M,
        2492771983119699473909219075M,
        273786986105352M,
        77032289288348745783788830720M,
        76751116212710868666717568743M,
        77660100872000274757728589805M,
        78311872300709847182133434640M,
        63719858631291361325338782444M,
        76744995638298187908657115625M,
        76755847766182644166514239727M,
        3111817783067066120628272896M,
        78595856477056358568720728579M,
        76448841943061186982118754824M,
        3111699426954233083478009329M,
        1234365448605371391053401874M,
        934490139827124963453109000M,
        77674711873460558861680378125M,
        77674621870640765137665717752M,
        75509502026317406343823557120M,
        6478728100488619770030582788M,
        10889026419746096040583506208M,
        8953475679622136593207462163M,
        5272154126808224640347877139M,
        70833234582273835254464251146M,
        4939590302649966837639140842M,
        5273386798208430121659199236M,
        10244565343686350222354812695M,
        78587380051522920198198657818M,
        1254926483030078602236859659M,
        1010161207860780319109115M
    };
}
