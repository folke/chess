using System;
using System.Collections.Generic;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    private readonly int maxDepth = 4;
    private readonly int quiescenceDepth = 3;
    private int evals;
    private readonly int[][] mg_pesto_table = Unpack(mg_pesto_packed);
    private readonly int[][] eg_pesto_table = Unpack(eg_pesto_packed);
    private readonly int[] mg_value = { 82, 337, 365, 477, 1025, 0 };
    private readonly int[] eg_value = { 94, 281, 297, 512, 936, 0 };
    private readonly int[] gamephaseInc = { 0, 1, 1, 2, 4, 0 };
    private Move[,] killerTable = new Move[64, 2]; // killer moves for each depth

    public Move Think(Board board, Timer timer)
    {
        killerTable = new Move[64, 2]; // killer moves for each depth
        evals = 0;
        double current = EvaluateBoard(board);
        Console.WriteLine($"MyBot: {current / 100.0}");

        Move bestMove = Move.NullMove;
        double bestScore = double.NegativeInfinity;

        Move[] moves = board.GetLegalMoves();
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            double score = -AlphaBeta(
                double.NegativeInfinity,
                double.PositiveInfinity,
                maxDepth - 1,
                false,
                board
            );
            board.UndoMove(move);
            // write move and score to console
            Console.WriteLine($"{move} {score / 100.0}");

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }
        Console.WriteLine($"MyBot: {bestMove} ({bestScore / 100.0}) in {evals} evals");

        return bestMove;
    }

    private double AlphaBeta(double alpha, double beta, int depth, bool quiescence, Board board)
    {
        if (quiescence)
        {
            double standPat = EvaluateBoard(board);
            if (depth == 0)
                return standPat;
            if (standPat >= beta)
                return beta;
            if (alpha < standPat)
                alpha = standPat;
        }
        else if (depth == 0)
            return AlphaBeta(alpha, beta, quiescenceDepth - 1, true, board);

        Move[] moves = board.GetLegalMoves(quiescence);
        if (!quiescence)
            moves = OrderMoves(moves, depth); // apply move ordering
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            double score = -AlphaBeta(
                -beta,
                -alpha,
                quiescence && board.IsInCheck() ? depth : depth - 1,
                quiescence,
                board
            );
            board.UndoMove(move);

            if (score >= beta)
            {
                if (!quiescence)
                {
                    // if the move is not a capture (to avoid polluting the killer table with tactical moves)
                    if (!move.IsCapture)
                    {
                        // Update the killer table
                        if (killerTable[depth, 0] != move)
                        {
                            killerTable[depth, 1] = killerTable[depth, 0];
                            killerTable[depth, 0] = move;
                        }
                    }
                }
                return quiescence ? beta : score; // Beta cut-off
            }
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
            return board.IsWhiteToMove ? -1000000 : 1000000;

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
        int mgPhase = Math.Min(gamePhase, 24); /* in case of early promotion */
        int egPhase = 24 - mgPhase;
        return (mgScore * mgPhase + egScore * egPhase) / 24;
    }

    public static int[][] Unpack(ulong[] packed)
    {
        int[][] unpacked = new int[6][];
        for (int i = 0; i < 6; i++)
        {
            unpacked[i] = new int[64];
            for (int j = 0; j < 64; j++)
            {
                unpacked[i][j] = BitConverter.ToInt16(
                    BitConverter.GetBytes(
                        (ushort)((packed[i * 16 + j / 4] >> (16 * (j % 4))) & 0xFFFF)
                    )
                );
            }
        }
        return unpacked;
    }

    private Move[] OrderMoves(Move[] moves, int depth)
    {
        // Simple move ordering using the killer heuristic
        // First, try moves that are stored in the killer table for the current depth
        // Afterwards, try the other moves

        List<Move> orderedMoves = new List<Move>();
        if (killerTable[depth, 0] != null && moves.Contains(killerTable[depth, 0]))
            orderedMoves.Add(killerTable[depth, 0]);
        if (killerTable[depth, 1] != null && moves.Contains(killerTable[depth, 1]))
            orderedMoves.Add(killerTable[depth, 1]);
        orderedMoves.AddRange(moves.Except(orderedMoves));

        return orderedMoves.ToArray();
    }

    public static ulong[] mg_pesto_packed =
    {
        0,
        0,
        26740384789299298,
        18443647995002880068,
        8725835947704314,
        18441114681553190977,
        5911000281645042,
        18440270222260437015,
        3659157517303781,
        18439707242241851409,
        18444210786034057190,
        18443366515723141123,
        18440551542617538525,
        18440551787432312817,
        0,
        0,
        18433233133087752025,
        18416907666042323005,
        10133412691574711,
        18441959029174304791,
        18296032403980241,
        12385212516335700,
        14918255371223031,
        6192526801567781,
        3659243417042931,
        18444492364091424796,
        2814805601157097,
        18442240581457477651,
        18446181076508082147,
        18441677464000462847,
        18437736629640363927,
        18440551546910736367,
        18436610622360977379,
        18444492308252917735,
        18443366296680726502,
        18433514827117428766,
        11259183754510320,
        18446181282673197091,
        14073830440304636,
        18446181153823326245,
        7318405229969402,
        1125942857302050,
        4222189076152320,
        2814827078287374,
        68720459780,
        281616712007687,
        18441114518340632543,
        18441114410965860339,
        14355361253949472,
        12103557143134271,
        17451697666261019,
        12385010648809552,
        10133210832044027,
        4503861623324689,
        7318383753560040,
        18441396014794604568,
        18446744026463272924,
        18440270179309518857,
        18442240409656098771,
        18437736852979974147,
        18444492192290504660,
        18427040799570788351,
        4785083193229293,
        18439707040378454032,
        3377824274644964,
        12666558638456891,
        562932771061736,
        15199769005260784,
        2251834172375027,
        16044275539640349,
        18442521884632678373,
        562941364666367,
        18444210764557778935,
        18445899665959092222,
        18446462551488397298,
        1407435013292027,
        563001492570077,
        562937069502472,
        3096190382964735,
        18432951671000137713,
        18442522017779941311,
        3659187579977672,
        18445055142244843549,
        18438862615447666680,
        18442240482673754103,
        18440551718711656428,
        18439425677069189103,
        18436892393688530914,
        18436047912925396943,
        18432670187432247250,
        18434077609562406898,
        18439425664183631828,
        18429011115817500673,
        2251842762375125,
        18431544476509208561,
        3940757046296584
    };
    public static ulong[] eg_pesto_packed =
    {
        0,
        0,
        37718325495398578,
        52636529323147411,
        18859188518387806,
        23644250234486840,
        1407430719701024,
        4785147618852862,
        18445055210964975629,
        18446462615912251385,
        562924184076292,
        18444773748872249344,
        2814784127369229,
        18444773757462511629,
        0,
        0,
        18439144197796331462,
        18419159259702231009,
        18446462495653167079,
        18432388751111487479,
        2533322033790952,
        18435484997331189759,
        6192543977177071,
        18441677558489219094,
        7036947431882734,
        18441677541309743120,
        4503599627239401,
        18440833017594052618,
        18445618135146758102,
        18434640555220467710,
        18442803329543045091,
        18429010939722268650,
        18444773705921593330,
        18440270080525205497,
        18443366408348565496,
        18443084886126624765,
        18446462603027283970,
        1125899907301374,
        2533326330658813,
        562962838978574,
        5348080392339450,
        18444492261011619847,
        2814788421681140,
        18442803393971027981,
        18446744047938633714,
        18439425664184942596,
        18445618079312904169,
        18442240456901328887,
        4222201960726541,
        1407409244078092,
        3096280579244043,
        844459290132477,
        1407404948783111,
        18446181106576064516,
        281530811482116,
        844420635230210,
        1125934266908675,
        18443929293877346299,
        18446744052234780668,
        18442521918993399801,
        562954248060922,
        18446181080805933047,
        18446462611617939447,
        18441114595649388539,
        7599918861975543,
        5629542485131291,
        11540611485466607,
        128850657338,
        13792312513986540,
        2533356397068335,
        12666477032636419,
        10133343977340985,
        13229405511679982,
        6474091970297887,
        1688918578036720,
        1407417834340361,
        18442521824503398378,
        18438018199106224112,
        18434922034491621343,
        18435484993034715131,
        18441958926088798134,
        18441959016286388213,
        4785134734802932,
        3096323530555409,
        4222223436021770,
        3659363678748692,
        7599927451910136,
        844536601444378,
        6755493930139630,
        18443647887621947419,
        5911026050400237,
        18444210828984975383,
        3659196171419621,
        18442240452607606798,
        18443929238040936395,
        18434922025902604260
    };
}
