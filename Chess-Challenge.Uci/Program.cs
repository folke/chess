﻿using ChessChallenge.Application;

namespace Chess_Challenge.Cli
{
    internal class Program
    {
        static int GetTokenCount()
        {
            string path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "src",
                "My Bot",
                "MyBot.cs"
            );
            string txt = File.ReadAllText(path);
            return TokenCounter.CountTokens(txt).totalCount;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Sebastian Lague's Chess Challenge submission by Gediminas Masaitis");
            var tokenCount = GetTokenCount();
            Console.WriteLine($"Current token count: {tokenCount}");
            Console.WriteLine();

            var uci = new Uci();
            uci.Run();
        }
    }
}
