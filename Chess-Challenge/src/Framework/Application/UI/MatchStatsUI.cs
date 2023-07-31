using Raylib_cs;
using System.Numerics;
using System;

namespace ChessChallenge.Application
{
    public static class MatchStatsUI
    {
        public static void DrawMatchStats(ChallengeController controller)
        {
            if (controller.PlayerWhite.IsBot && controller.PlayerBlack.IsBot)
            {
                int nameFontSize = UIHelper.ScaleInt(40);
                int regularFontSize = UIHelper.ScaleInt(35);
                int headerFontSize = UIHelper.ScaleInt(45);
                Color col = new(180, 180, 180, 255);
                Vector2 startPos = UIHelper.Scale(new Vector2(1500, 250));
                float spacingY = UIHelper.Scale(35);

                DrawNextText($"Game {controller.CurrGameNumber} of {controller.TotalGameCount}", headerFontSize, Color.WHITE);
                startPos.Y += spacingY * 2;

                DrawStats(controller.BotStatsA);
                startPos.Y += spacingY * 2;
                DrawStats(controller.BotStatsB);
           

                void DrawStats(ChallengeController.BotMatchStats stats)
                {
                    MyBotStats myStats = new MyBotStats(stats.NumWins, stats.NumLosses, stats.NumDraws);
                    DrawNextText(stats.BotName + ":", nameFontSize, Color.WHITE);

                    double winning = myStats.WinningFraction() * 100;
                    DrawNextText($"Winning: {winning:F0}%", regularFontSize, winning > 50 ? Color.GREEN : Color.RED);
                    DrawNextText($"Score: +{stats.NumWins} ={stats.NumDraws} -{stats.NumLosses}", regularFontSize, col);
                    double elo = myStats.EloDifference();
                    DrawNextText($"Elo: {elo:+0;-0;0} +/-{myStats.EloErrorMargin():F0}", regularFontSize, col);
                    DrawNextText($"LOS: {myStats.LOS() * 100:0}%", regularFontSize, col);
                    MyBotStats.SprtTest sprt = myStats.Sprt();
                    DrawNextText($"SPRT: {sprt}", UIHelper.ScaleInt(25), sprt.hypothesis == MyBotStats.Hypothesis.H0 ? Color.RED : sprt.hypothesis == MyBotStats.Hypothesis.H1 ? Color.GREEN : col);

                    if (stats.NumTimeouts > 0)
                        DrawNextText($"Timeouts: {stats.NumTimeouts}", regularFontSize, Color.RED);
                    if (stats.NumIllegalMoves > 0)
                        DrawNextText($"Illegal Moves: {stats.NumIllegalMoves}", regularFontSize, Color.RED);
                }
           
                void DrawNextText(string text, int fontSize, Color col)
                {
                    UIHelper.DrawText(text, startPos, fontSize, 1, col);
                    startPos.Y += spacingY;
                }
            }
        }
    }
}
