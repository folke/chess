using ChessChallenge.API;
using System;

namespace ChessChallenge.Example
{
    public class EvilBot : IChessBot
    {
        // Piece values: null, pawn, knight, bishop, rook, queen, king
        int CHECKMATE = 100000;
        Board board;
        Timer timer;
        int time_limit = 0;
        Move depth_move = new Move();
        Int64 nodes = 0;

        public Move Think(Board board_input, Timer timer_input)
        {
            board = board_input;
            timer = timer_input;
            return Iterative_Deepening();
        }

        public Move Iterative_Deepening()
        {
            nodes = 0;
            time_limit = 100; //timer.MillisecondsRemaining / 2000;

            Move[] moves = board.GetLegalMoves();
            Move best_move = moves[0];

            for (int depth = 1; depth < 100; depth++)
            {
                depth_move = moves[0];
                int score = Negamax(depth, 0, -CHECKMATE, CHECKMATE);

                if (timer.MillisecondsElapsedThisTurn > time_limit)
                    break;

                best_move = depth_move;

                // Console.WriteLine(String.Format("depth {0} score {1} nodes {2} nps {3} time {4} pv {5}{6}",
                //     depth,
                //     score,
                //     nodes,
                //     (Int64)(1000 * nodes / (timer.MillisecondsElapsedThisTurn + 1)),
                //     timer.MillisecondsElapsedThisTurn,
                //     best_move.StartSquare.Name,
                //     best_move.TargetSquare.Name
                // ));

                if (score > CHECKMATE / 2)
                    break;
            }
            // Console.WriteLine();

            return best_move;
        }

        public int Negamax(int depth, int ply, int alpha, int beta)
        {
            nodes++;

            bool root = ply == 0;
            bool q_search = depth <= 0;
            ulong key = board.ZobristKey;

            if (timer.MillisecondsElapsedThisTurn > time_limit)
                return 0;
            if (!root && board.IsRepeatedPosition())
                return -20;

            // Delta Pruning
            if (q_search)
            {
                int eval = Eval();
                if (eval >= beta)
                    return beta;
                // if (eval < alpha - 1025) return alpha;
                if (eval > alpha)
                    alpha = eval;
            }

            Move[] moves = board.GetLegalMoves(q_search);
            foreach (Move move in moves)
            {
                board.MakeMove(move);
                int new_score = -Negamax(depth - 1, ply + 1, -beta, -alpha);
                board.UndoMove(move);

                if (new_score > alpha)
                {
                    if (root)
                        depth_move = move;
                    if (new_score >= beta)
                        return beta;
                    alpha = Math.Max(alpha, new_score);
                }
            }

            if (!q_search && moves.Length == 0)
            {
                return board.IsInCheck() ? -CHECKMATE + ply : 0;
            }

            return alpha;
        }

        // PeSTO Evaluation Function
        readonly int[] pvm_mg = { 0, 82, 337, 365, 477, 1025, 20000 };
        readonly int[] pvm_eg = { 0, 94, 281, 297, 512, 936, 20000 };
        readonly int[] phase_weight = { 0, 0, 1, 1, 2, 4, 0 };

        // thanks for the compressed pst implementation https://github.com/JacquesRW
        readonly ulong[] pst_compressed =
        {
            657614902731556116,
            420894446315227099,
            384592972471695068,
            312245244820264086,
            364876803783607569,
            366006824779723922,
            366006826859316500,
            786039115310605588,
            421220596516513823,
            366011295806342421,
            366006826859316436,
            366006896669578452,
            162218943720801556,
            440575073001255824,
            657087419459913430,
            402634039558223453,
            347425219986941203,
            365698755348489557,
            311382605788951956,
            147850316371514514,
            329107007234708689,
            402598430990222677,
            402611905376114006,
            329415149680141460,
            257053881053295759,
            291134268204721362,
            492947507967247313,
            367159395376767958,
            384021229732455700,
            384307098409076181,
            402035762391246293,
            328847661003244824,
            365712019230110867,
            366002427738801364,
            384307168185238804,
            347996828560606484,
            329692156834174227,
            365439338182165780,
            386018218798040211,
            456959123538409047,
            347157285952386452,
            365711880701965780,
            365997890021704981,
            221896035722130452,
            384289231362147538,
            384307167128540502,
            366006826859320596,
            366006826876093716,
            366002360093332756,
            366006824694793492,
            347992428333053139,
            457508666683233428,
            329723156783776785,
            329401687190893908,
            366002356855326100,
            366288301819245844,
            329978030930875600,
            420621693221156179,
            422042614449657239,
            384602117564867863,
            419505151144195476,
            366274972473194070,
            329406075454444949,
            275354286769374224,
            366855645423297932,
            329991151972070674,
            311105941360174354,
            256772197720318995,
            365993560693875923,
            258219435335676691,
            383730812414424149,
            384601907111998612,
            401758895947998613,
            420612834953622999,
            402607438610388375,
            329978099633296596,
            67159620133902
        };

        public int Get_Pst_Bonus(int psq)
        {
            return (int)(((pst_compressed[psq / 10] >> (6 * (psq % 10))) & 63) - 20) * 8;
        }

        public int Eval()
        {
            int turn = Convert.ToInt32(board.IsWhiteToMove);
            int[] score_mg = { 0, 0 };
            int[] score_eg = { 0, 0 };
            int phase = 0;

            foreach (bool side in new[] { false, true })
            {
                for (int piece_type = 1; piece_type <= 6; piece_type++)
                {
                    ulong bb = board.GetPieceBitboard((PieceType)piece_type, side);
                    while (bb > 0)
                    {
                        int index =
                            128 * (piece_type - 1) + BitboardHelper.ClearAndGetIndexOfLSB(ref bb)
                            ^ (side ? 56 : 0);
                        score_mg[Convert.ToInt32(side)] +=
                            pvm_mg[piece_type] + Get_Pst_Bonus(index);
                        score_eg[Convert.ToInt32(side)] +=
                            pvm_eg[piece_type] + Get_Pst_Bonus(index + 64);
                        phase += phase_weight[piece_type];
                    }
                }
            }

            score_mg[turn] += 14;

            // In case of premature promotion
            phase = Math.Min(phase, 24);

            return (
                    (score_mg[turn] - score_mg[turn ^ 1]) * phase
                    + (score_eg[turn] - score_eg[turn ^ 1]) * (24 - phase)
                ) / 24;
        }
    }
}
