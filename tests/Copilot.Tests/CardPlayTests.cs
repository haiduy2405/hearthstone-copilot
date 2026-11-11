using System.Linq;
using Copilot.Engine;
using Copilot.Engine.Lethal;
using Copilot.Engine.Model;
using Xunit;

namespace Copilot.Tests
{
    public class CardPlayTests
    {
        private static int _id = 1000;

        private static MinionState Mine(int atk, int hp, int attacks = 1) =>
            new MinionState { Id = _id++, Name = "M" + atk + "/" + hp, Attack = atk, Health = hp, AttacksRemaining = attacks, Support = Support.Supported };

        private static MinionState Taunt(int atk, int hp, int drArmor = 0) =>
            new MinionState { Id = _id++, Name = "T" + atk + "/" + hp, Attack = atk, Health = hp, Taunt = true, DeathrattleArmor = drArmor, Support = Support.KeywordOnly };

        private static HandCard Spell(string cardId, string name, int cost) =>
            new HandCard { Id = _id++, CardId = cardId, Name = name, Cost = cost, Kind = CardKind.Spell };

        private static HandCard Minion(string cardId, string name, int cost, int atk, int hp) =>
            new HandCard { Id = _id++, CardId = cardId, Name = name, Cost = cost, Kind = CardKind.Minion, Attack = atk, Health = hp };

        private static HandCard Filler(int cost) =>
            new HandCard { Id = _id++, CardId = "FILLER", Name = "Filler" + cost, Cost = cost, Kind = CardKind.Minion, Attack = 1, Health = 1 };

        private static GameSnapshot Snap(int oppHp, int mana)
        {
            var s = new GameSnapshot { MyTurn = true };
            s.Me.HeroHealth = 30;
            s.Me.Mana = mana;
            s.Opp.HeroHealth = oppHp;
            return s;
        }

        private static HandCard Blasphemy(int cost = 4) => Spell("BE_086", "Blasphemy", cost);

        [Fact]
        public void Blasphemy_Doubled_ClearsTaunt_ThenAttackWins()
        {
            var s = Snap(6, mana: 4);
            s.Me.Board.Add(Mine(3, 3));
            s.Opp.Board.Add(Taunt(1, 4));
            s.Me.Hand.Add(Blasphemy());
            var r = LethalSolver.Solve(s);
            Assert.Equal(LethalStatus.Proven, r.Status);
            Assert.Equal(ActionKind.Play, r.Actions.First().Kind);
            Assert.Equal(7, r.FaceDamage); // 4 burn + one 3-power swing (only 2 were still missing)
            Assert.Equal(6, r.RequiredDamage);
        }

        [Fact]
        public void Blasphemy_TieOnHighestCost_IsNotDoubled()
        {
            var s = Snap(6, mana: 4);
            s.Me.Board.Add(Mine(3, 3));
            s.Opp.Board.Add(Taunt(1, 4));
            s.Me.Hand.Add(Blasphemy());
            s.Me.Hand.Add(Filler(4)); // ties: modelled as single cast
            Assert.Equal(LethalStatus.None, LethalSolver.Solve(s).Status);

            s.Me.Hand[1].Cost = 3; // strictly highest -> doubled
            Assert.Equal(LethalStatus.Proven, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void NotEnoughMana_NoLethal()
        {
            var s = Snap(6, mana: 3);
            s.Me.Board.Add(Mine(3, 3));
            s.Opp.Board.Add(Taunt(1, 4));
            s.Me.Hand.Add(Blasphemy());
            Assert.Equal(LethalStatus.None, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void Tachyon_OnlyDeterministicWithEmptyEnemyBoard_AndCappedAtLikely()
        {
            var s = Snap(6, mana: 2);
            s.Me.Hand.Add(Spell("TIME_027", "Tachyon Barrage", 2));
            var r = LethalSolver.Solve(s);
            Assert.Equal(LethalStatus.Likely, r.Status);
            Assert.Contains(r.Reasons, x => x.Contains("Unverified rule"));

            s.Opp.Board.Add(new MinionState { Id = 5, Name = "Bystander", Attack = 1, Health = 1, Support = Support.Supported });
            Assert.Equal(LethalStatus.None, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void EntropicContinuity_AddsOneAttackPerMinion()
        {
            var s = Snap(9, mana: 1);
            s.Me.Board.Add(Mine(2, 2)); s.Me.Board.Add(Mine(2, 2)); s.Me.Board.Add(Mine(2, 2));
            s.Me.Hand.Add(Spell("TIME_026", "Entropic Continuity", 1));
            Assert.Equal(LethalStatus.Proven, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void SpellCostTax_MakesLethalIllegal()
        {
            var s = Snap(9, mana: 1);
            s.Me.Board.Add(Mine(2, 2)); s.Me.Board.Add(Mine(2, 2)); s.Me.Board.Add(Mine(2, 2));
            s.Me.Hand.Add(Spell("TIME_026", "Entropic Continuity", 2)); // Mawman Cultist aura: +1
            Assert.Equal(LethalStatus.None, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void RushMinion_ClearsTaunt_ButNeedsHeroHealthMargin()
        {
            var s = Snap(3, mana: 5);
            s.Me.Board.Add(Mine(3, 3));
            s.Opp.Board.Add(Taunt(1, 5));
            s.Me.Hand.Add(Minion("TIME_029", "Ruinous Velocidrake", 5, 5, 5));
            Assert.Equal(LethalStatus.Proven, LethalSolver.Solve(s).Status);

            s.Me.HeroHealth = 3; // battlecry may cast a Shred of Time: 3 damage to our own hero
            Assert.Equal(LethalStatus.None, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void Vaultkeeper_BuffsAdjacent_AndSlotChoiceMatters()
        {
            var s = Snap(9, mana: 5);
            s.Me.Board.Add(Mine(3, 3)); s.Me.Board.Add(Mine(2, 2));
            s.Me.Hand.Add(Minion("BE_151", "Vaultkeeper", 5, 3, 3));
            var r = LethalSolver.Solve(s);
            Assert.Equal(LethalStatus.Proven, r.Status); // 5 + (+2 +2 between them) = 9
            var play = r.Actions.First(a => a.Kind == ActionKind.Play);
            Assert.Equal(1, play.Slot);
        }

        [Fact]
        public void FullBoard_CannotPlayMinion()
        {
            var s = Snap(9, mana: 5);
            for (int i = 0; i < 7; i++) s.Me.Board.Add(Mine(1, 1));
            s.Me.Hand.Add(Minion("BE_151", "Vaultkeeper", 5, 3, 3));
            Assert.Equal(LethalStatus.None, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void EnemyDeathrattleArmor_RaisesRequirement()
        {
            var s = Snap(4, mana: 0);
            s.Me.Board.Add(Mine(1, 5));
            s.Me.Board.Add(Mine(4, 4));
            s.Opp.Board.Add(Taunt(0, 1, drArmor: 3));
            // Killing the Taunt gives +3 armor: need 7, only 4 left to go face.
            Assert.Equal(LethalStatus.None, LethalSolver.Solve(s).Status);

            s.Me.Board.Add(Mine(3, 3));
            var r = LethalSolver.Solve(s);
            Assert.Equal(LethalStatus.Proven, r.Status);
            Assert.Equal(7, r.RequiredDamage);
        }

        [Fact]
        public void FloatingLessons_UsesCurrentCost_AndIsCappedAtLikely()
        {
            var s = Snap(5, mana: 1);
            s.Me.Board.Add(Mine(3, 3));
            s.Me.Hand.Add(Spell("BE_094", "Floating Lessons", 1));
            var r = LethalSolver.Solve(s);
            Assert.Equal(LethalStatus.Likely, r.Status); // 3 + 2 = 5
        }

        [Fact]
        public void FewestCardsPlayed_IsPreferred()
        {
            var s = Snap(3, mana: 10);
            s.Me.Board.Add(Mine(3, 3));
            s.Me.Hand.Add(Spell("TIME_026", "Entropic Continuity", 1));
            var r = LethalSolver.Solve(s);
            Assert.Equal(LethalStatus.Proven, r.Status);
            Assert.DoesNotContain(r.Actions, a => a.Kind == ActionKind.Play);
        }

        [Fact]
        public void Presenter_ShowsPlayAndAttackSteps()
        {
            var s = Snap(6, mana: 4);
            s.Me.Board.Add(Mine(3, 3));
            s.Opp.Board.Add(Taunt(1, 4));
            s.Me.Hand.Add(Blasphemy());
            var text = Presenter.Format(s, LethalSolver.Solve(s));
            Assert.Contains("Play Blasphemy", text);
            Assert.Contains("FACE", text);
        }

        [Fact]
        public void Threat_FlagsDangerFromVisibleBoardOnly()
        {
            var s = Snap(30, mana: 0);
            s.Me.HeroHealth = 8;
            s.Opp.Board.Add(new MinionState { Id = 1, Attack = 5, Health = 5, MaxAttacks = 1 });
            s.Opp.Board.Add(new MinionState { Id = 2, Attack = 2, Health = 2, MaxAttacks = 2 });
            s.Opp.Board.Add(new MinionState { Id = 3, Attack = 9, Health = 9, MaxAttacks = 1, Frozen = true });
            var t = ThreatAnalyzer.Analyze(s);
            Assert.Equal(9, t.VisibleDamage); // 5 + 2*2, frozen minion ignored
            Assert.True(t.Danger);
            s.Me.HeroHealth = 30;
            Assert.False(ThreatAnalyzer.Analyze(s).Danger);
        }
    }
}
