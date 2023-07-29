// LMR
/* var DoSearch = (int reduction) => */
/*     -Search( */
/*         -beta, */
/*         -alpha, */
/*         // Check extension, but only if we're not in quiescence search */
/*         board.IsInCheck() */
/*             ? depth */
/*             : depth - 1 - reduction, */
/*         ply + 1 */
/*     ); */
/* int reduction = moveCount++ >= 3 && depth > 2 && !move.IsCapture ? 1 : 0; */
/**/
/* double score = DoSearch(reduction); */
/* if (reduction > 0 && score > alpha) */
/*     score = DoSearch(0); */


// Negascout
/* double score; */
/* if (isFirstMove) */
/* { */
/*     score = -Search(-beta, -alpha, ply - 1); */
/*     isFirstMove = false; */
/* } */
/* else */
/* { */
/*     score = -Search(-alpha - 1, -alpha, ply - 1); // Null-window search */
/*     if (alpha < score && score < beta) // if it failed high */
/*         score = -Search(-beta, -score, ply - 1); // do a full re-search */
/* } */


// Aspiration windows

/* double alpha = -32001; */
/* double beta = 32001; */
/* double aspirationWindow = 50; */
/* try */
/* { */
/*     for (searchDepth = 1; searchDepth <= maxDepth; searchDepth++) */
/*     { */
/*         double bestScore = Search(alpha, beta, searchDepth, 0); */
/*         if (bestScore <= alpha || bestScore >= beta) */
/*         { */
/*             Console.WriteLine("Aspiration window fail"); */
/**/
/*             // If score is out of bounds, re-search with a full window */
/*             alpha = -32001; */
/*             beta = 32001; */
/*             bestScore = Search(alpha, beta, searchDepth, 0); */
/*         } */
/*         if (bestScore > 9000) */
/*             break; */
/**/
/*         // Narrow the window for the next iteration */
/*         alpha = bestScore - aspirationWindow; */
/*         beta = bestScore + aspirationWindow; */
/*     } */
/* } */
/* catch (TimeoutException) { } */
