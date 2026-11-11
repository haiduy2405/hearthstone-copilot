using System.Diagnostics;
using Copilot.Engine.Lethal;
using Copilot.Engine.Model;
using Xunit;
using Xunit.Abstractions;

namespace Copilot.Tests
{
    /// <summary>Worst-case latency guard. Numbers are printed so progress.md can quote real measurements.</summary>
    public class BenchmarkTests
    {
        private readonly ITestOutputHelper _out;
        public BenchmarkTests(ITestOutputHelper o) { _out = o; }

        private static GameSnapshot Heavy(int oppHp)
        {
            var s = new GameSnapshot { MyTurn = true };
            s.Me.HeroHealth = 30; s.Me.Mana = 10;
            s.Opp.HeroHealth = oppHp;
            int id = 1;
            for (int i = 0; i < 6; i++)
                s.Me.Board.Add(new MinionState { Id = id++, Name = "A" + i, Attack = 2 + i % 3, Health = 3, AttacksRemaining = 1 + (i % 2), Support = Support.Supported });
            for (int i = 0; i < 4; i++)
                s.Opp.Board.Add(new MinionState { Id = id++, Name = "T" + i, Attack = 2, Health = 3 + i, Taunt = true, Support = Support.KeywordOnly });
            s.Me.Hand.Add(new HandCard { Id = id++, CardId = "BE_086", Name = "Blasphemy", Cost = 4, Kind = CardKind.Spell });
            s.Me.Hand.Add(new HandCard { Id = id++, CardId = "TIME_027", Name = "Tachyon", Cost = 2, Kind = CardKind.Spell });
            s.Me.Hand.Add(new HandCard { Id = id++, CardId = "TIME_026", Name = "Entropic", Cost = 1, Kind = CardKind.Spell });
            s.Me.Hand.Add(new HandCard { Id = id++, CardId = "BE_094", Name = "Floating", Cost = 1, Kind = CardKind.Spell });
            s.Me.Hand.Add(new HandCard { Id = id++, CardId = "BE_151", Name = "Vault", Cost = 5, Kind = CardKind.Minion, Attack = 3, Health = 3 });
            s.Me.Hand.Add(new HandCard { Id = id++, CardId = "TIME_029", Name = "Velo", Cost = 5, Kind = CardKind.Minion, Attack = 5, Health = 5 });
            return s;
        }

        [Theory]
        [InlineData(200)] // impossible: forces the search to run to its budget or exhaustion
        [InlineData(30)]
        [InlineData(18)]
        public void WorstCase_StaysUnderOneSecond(int oppHp)
        {
            var s = Heavy(oppHp);
            LethalSolver.Solve(s); // warm-up (JIT)
            var sw = Stopwatch.StartNew();
            var r = LethalSolver.Solve(s);
            sw.Stop();
            _out.WriteLine($"oppHp={oppHp} status={r.Status} nodes={r.NodesVisited} ms={sw.ElapsedMilliseconds}");
            Assert.True(sw.ElapsedMilliseconds < 1000, "took " + sw.ElapsedMilliseconds + " ms");
        }
    }
}
