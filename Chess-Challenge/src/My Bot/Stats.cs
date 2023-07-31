using System;

public class MyBotStats
{
    public enum Hypothesis
    {
        H0,
        H1,
        None
    }

    public struct SprtTest
    {
        public Hypothesis hypothesis;
        public double llr;
        public double la;
        public double lb;

        public override readonly string ToString()
        {
            return hypothesis == Hypothesis.None
                ? $"{hypothesis} ({Progress() * 100:0}%), {la:F2} < {llr:F2} < {lb:F2}"
                : hypothesis.ToString();
        }

        public readonly double Progress()
        {
            double da = Math.Abs(llr - la);
            double db = Math.Abs(llr - lb);
            return 1.0 - (da < db ? da : db) / ((lb - la) / 2.0);
        }
    }

    public int Wins { get; }
    public int Losses { get; }
    public int Draws { get; }

    public MyBotStats(int wins, int losses, int draws)
    {
        Wins = wins;
        Losses = losses;
        Draws = draws;
    }

    public double WinningFraction()
    {
        return (Wins + 0.5 * Draws) / Games();
    }

    public double EloDifference(double? winningFraction = null)
    {
        winningFraction ??= WinningFraction();
        return -Math.Log(1.0 / (double)winningFraction - 1.0) * 400.0 / Math.Log(10.0);
    }

    public double EloErrorMargin()
    {
        double total = Wins + Draws + Losses;
        double winP = Wins / total;
        double drawP = Draws / total;
        double lossP = Losses / total;

        double percentage = (Wins + Draws / 2.0) / total;
        double winDev = winP * Math.Pow(1 - percentage, 2);
        double drawsDev = drawP * Math.Pow(0.5 - percentage, 2);
        double lossesDev = lossP * Math.Pow(0 - percentage, 2);

        double stdDeviation = Math.Sqrt(winDev + drawsDev + lossesDev) / Math.Sqrt(total);

        double confidenceP = 0.95;
        double minConfidenceP = (1 - confidenceP) / 2;
        double maxConfidenceP = 1 - minConfidenceP;
        double devMin = percentage + PhiInv(minConfidenceP) * stdDeviation;
        double devMax = percentage + PhiInv(maxConfidenceP) * stdDeviation;

        double difference = EloDifference(devMax) - EloDifference(devMin);
        double margin = Math.Round(difference / 2);
        return double.IsNaN(margin) ? 0 : margin;
    }

    private static double PhiInv(double p)
    {
        return Math.Sqrt(2) * ErfInv(2 * p - 1);
    }

    private static double ErfInv(double x)
    {
        double a = 8 * (Math.PI - 3) / (3 * Math.PI * (4 - Math.PI));
        double y = Math.Log(1 - x * x);
        double z = 2 / (Math.PI * a) + y / 2;

        double ret = Math.Sqrt(Math.Sqrt(z * z - y / a) - z);
        return x < 0 ? -ret : ret;
    }

    public double LOS()
    {
        return .5 + .5 * Erf((Wins - Losses) / Math.Sqrt(2.0 * (Wins + Losses)));
    }

    public int Games()
    {
        return Wins + Losses + Draws;
    }

    private static double Erf(double x)
    {
        // constants
        double a1 = 0.254829592;
        double a2 = -0.284496736;
        double a3 = 1.421413741;
        double a4 = -1.453152027;
        double a5 = 1.061405429;
        double p = 0.3275911;

        // Save the sign of x
        int sign = 1;
        if (x < 0)
            sign = -1;
        x = Math.Abs(x);

        // A&S formula 7.1.26
        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return sign * y;
    }

    public static double LL(double x)
    {
        return 1.0 / (1 + Math.Pow(10, -x / 400.0));
    }

    public double LLR(double elo0, double elo1)
    {
        if (Wins == 0 || Draws == 0 || Losses == 0)
        {
            return 0.0;
        }

        double N = Wins + Draws + Losses;
        double w = Wins / N;
        double d = Draws / N;

        double s = w + d / 2;
        double m2 = w + d / 4;

        double variance = m2 - Math.Pow(s, 2);
        double var_s = variance / N;

        double s0 = LL(elo0);
        double s1 = LL(elo1);

        return (s1 - s0) * (2 * s - s0 - s1) / var_s / 2.0;
    }

    /*
    This function sequentially tests two hypotheses:
    H0: The Elo rating difference between two versions of the bot is elo0,
    H1: The Elo rating difference between two versions of the bot is elo1.
    The test is conducted based on the win/draw/loss record of the two versions against each other.

    Parameters:
        elo0: The assumed Elo rating difference under hypothesis H0. Default is 0.
        elo1: The assumed Elo rating difference under hypothesis H1. Default is 10.
        alpha: The probability of rejecting H0 when it is true (false positive). Default is 0.05.
        beta: The probability of accepting H0 when it is false (false negative). Default is 0.05.

    Returns:
        Hypothesis.H0 if the evidence supports hypothesis H0.
        Hypothesis.H1 if the evidence supports hypothesis H1.
        Hypothesis.None if neither hypothesis is strongly supported.

    Note:
        - The defaults assume that you are interested in detecting a small improvement (10 Elo points).
        - The values of alpha and beta control the trade-off between sensitivity and specificity of the test.
          Lower values make the test more conservative but might require more data to reach a conclusion.
    */
    public SprtTest Sprt(double elo0 = 0, double elo1 = 10, double alpha = 0.05, double beta = 0.05)
    {
        double llr = LLR(elo0, elo1);
        double la = Math.Log(beta / (1 - alpha));
        double lb = Math.Log((1 - beta) / alpha);

        return new SprtTest
        {
            hypothesis =
                llr > lb
                    ? Hypothesis.H1
                    : llr < la
                        ? Hypothesis.H0
                        : Hypothesis.None,
            llr = llr,
            la = la,
            lb = lb
        };
    }
}
