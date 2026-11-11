using System.Linq;
using Copilot.Engine;
using Copilot.Engine.Lethal;
using Copilot.Engine.Model;
using Xunit;

namespace Copilot.Tests
{
    public class MulliganAndChargeTests
    {
        private static int _id = 5000;

        private static HandCard C(string cardId, string name, int cost, CardKind kind = CardKind.Minion, int atk = 0, int hp = 0, string text = null) =>
            new HandCard { Id = _id++, CardId = cardId, Name = name, Cost = cost, Kind = kind, Attack = atk, Health = hp, Text = text };

        private static GameSnapshot Mull(bool onPlay, params HandCard[] cards)
        {
            var s = new GameSnapshot { InMulligan = true, OnThePlay = onPlay };
            s.Me.Hand.AddRange(cards);
            return s;
        }

        private static bool Kept(System.Collections.Generic.List<MulliganPick> p, string name) => p.First(x => x.Name == name).Keep;

        [Fact]
        public void HighWinRateCards_AreKept_LowOnesReplaced()
        {
            var s = Mull(true, C("BE_085", "Ritual of Summoning", 2, CardKind.Spell), C("TIME_025", "Timehopper", 2, atk: 4, hp: 4),
                         C("BE_729", "Mawman", 3, atk: 4, hp: 4), C("BE_151", "Vaultkeeper", 5, atk: 3, hp: 3));
            var p = MulliganAdvisor.Advise(s);
            Assert.True(Kept(p, "Ritual of Summoning"));
            Assert.True(Kept(p, "Timehopper"));
            Assert.False(Kept(p, "Mawman"));      // 59.7% < threshold
            Assert.False(Kept(p, "Vaultkeeper")); // 54.5%
        }

        [Fact]
        public void Threshold_IsInclusive_AndShownInReason()
        {
            var s = Mull(false, C("CORE_UNG_205", "Glacial Shard", 1, atk: 2, hp: 1), C("TIME_025", "Timehopper", 2, atk: 4, hp: 4));
            var p = MulliganAdvisor.Advise(s);
            Assert.False(Kept(p, "Glacial Shard")); // 63.4
            Assert.True(Kept(p, "Timehopper"));     // 65.4
            Assert.Contains("65.4", p.First(x => x.Name == "Timehopper").Reason);
        }

        [Fact]
        public void TheCoin_IsNeverListed()
        {
            var s = Mull(false, C("GAME_005", "The Coin", 0, CardKind.Spell), C("BE_726", "Ritual of C'Thun", 1, CardKind.Spell));
            var p = MulliganAdvisor.Advise(s);
            Assert.Single(p);
        }

        [Fact]
        public void UnknownCard_UsesCostRule()
        {
            var s = Mull(true, C("NEW_1", "Cheap", 2, atk: 2, hp: 2), C("NEW_2", "Big", 6, atk: 6, hp: 6));
            var p = MulliganAdvisor.Advise(s);
            Assert.True(Kept(p, "Cheap"));
            Assert.False(Kept(p, "Big"));
        }

        [Fact]
        public void Mulligan_PanelIsAutoExpanded_AndHeadlineCountsKeeps()
        {
            var s = Mull(true, C("TIME_025", "Timehopper", 2, atk: 4, hp: 4), C("BE_086", "Blasphemy", 4, CardKind.Spell), C("BE_729", "Mawman", 3, atk: 4, hp: 4));
            var v = Presenter.Build(s, LethalSolver.Solve(s));
            Assert.Equal(PanelLevel.Mulligan, v.Level);
            Assert.True(v.AutoExpand);
            Assert.Equal("KEEP 1 / SWAP 2", v.Headline);
            Assert.Contains("REPLACE", v.Details);
        }

        [Fact]
        public void Mulligan_EmptyHand_IsHidden()
        {
            var s = Mull(true);
            Assert.Equal(PanelLevel.Hidden, Presenter.Build(s, LethalSolver.Solve(s)).Level);
        }

        // ---- generic Charge / Rush bodies in hand ----

        private static GameSnapshot Turn(int oppHp, int mana)
        {
            var s = new GameSnapshot { MyTurn = true };
            s.Me.HeroHealth = 30; s.Me.Mana = mana; s.Opp.HeroHealth = oppHp;
            return s;
        }

        [Fact]
        public void ChargeMinionInHand_AddsFaceDamage()
        {
            var s = Turn(4, 4);
            s.Me.Hand.Add(C("UNKNOWN_CHARGE", "Charger", 4, atk: 4, hp: 2, text: "<b>Charge</b>"));
            var r = LethalSolver.Solve(s);
            Assert.Equal(LethalStatus.Proven, r.Status);
            Assert.Equal(ActionKind.Play, r.Actions.First().Kind);
        }

        [Fact]
        public void ChargeMinion_TooExpensive_IsNotLethal()
        {
            var s = Turn(4, 3);
            s.Me.Hand.Add(C("UNKNOWN_CHARGE", "Charger", 4, atk: 4, hp: 2, text: "<b>Charge</b>"));
            Assert.Equal(LethalStatus.None, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void RushMinion_CannotGoFace()
        {
            var s = Turn(4, 4);
            s.Me.Hand.Add(C("UNKNOWN_RUSH", "Rusher", 4, atk: 4, hp: 4, text: "<b>Rush</b>"));
            Assert.Equal(LethalStatus.None, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void RushMinion_ClearsTaunt_ForBoardAttackers()
        {
            var s = Turn(3, 4);
            s.Me.Board.Add(new MinionState { Id = 1, Name = "A", Attack = 3, Health = 3, AttacksRemaining = 1, Support = Support.Supported });
            s.Opp.Board.Add(new MinionState { Id = 2, Name = "T", Attack = 1, Health = 4, Taunt = true, Support = Support.KeywordOnly });
            s.Me.Hand.Add(C("UNKNOWN_RUSH", "Rusher", 4, atk: 4, hp: 4, text: "<b>Rush</b>"));
            var r = LethalSolver.Solve(s);
            Assert.Equal(LethalStatus.Proven, r.Status);
        }

        [Fact]
        public void MinionWithBattlecryText_IsNotTreatedAsPlainCharge()
        {
            var s = Turn(4, 4);
            s.Me.Hand.Add(C("UNKNOWN_BC", "Mystery", 4, atk: 4, hp: 2, text: "<b>Charge</b> <b>Battlecry:</b> Do something."));
            Assert.Equal(LethalStatus.None, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void Danger_DetailsMentionWhatIsNotCounted()
        {
            var s = Turn(30, 0);
            s.MyTurn = true; s.Me.HeroHealth = 6; s.Opp.Secrets = 1;
            s.Opp.Board.Add(new MinionState { Id = 9, Name = "Big", Attack = 7, Health = 7, MaxAttacks = 1 });
            var v = Presenter.Build(s, LethalSolver.Solve(s));
            Assert.Equal(PanelLevel.Danger, v.Level);
            Assert.Contains("Not counted", v.Details);
            Assert.Contains("secret", v.Details);
        }
    }
}
