// LMR
/* bool lmr = */
/*     doLMR */
/*     && moveCount++ >= 4 */
/*     && ply >= 2 */
/*     && !move.IsCapture */
/*     && !move.IsPromotion */
/*     && !board.IsInCheck(); */
/* double score = -Search(-beta, -alpha, searchDepth - (lmr ? 1 : 0), false, !lmr); */
/* if (lmr && score > alpha) // If score exceeds alpha, re-search without reduction */
/*     score = -Search(-beta, -alpha, searchDepth, false, true); */


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
