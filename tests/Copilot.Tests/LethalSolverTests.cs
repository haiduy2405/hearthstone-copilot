using System.Linq;
using System.Threading;
using Copilot.Engine.Lethal;
using Copilot.Engine.Model;
using Xunit;

namespace Copilot.Tests
{
    public class LethalSolverTests
    {
        private static int _id = 100;

        private static MinionState Mine(int atk, int hp, int attacks = 1, bool shield = false, bool poison = false, bool canHero = true, Support sup = Support.Supported) =>
            new MinionState { Id = _id++, Name = "M" + atk + "/" + hp, Attack = atk, Health = hp, AttacksRemaining = attacks, DivineShield = shield, Poisonous = poison, CanAttackHero = canHero, Support = sup };

        private static MinionState Taunt(int atk, int hp, bool shield = false, bool stealth = false, bool immune = false, Support sup = Support.Supported) =>
            new MinionState { Id = _id++, Name = "T" + atk + "/" + hp, Attack = atk, Health = hp, Taunt = true, DivineShield = shield, Stealth = stealth, Immune = immune, Support = sup };

        private static GameSnapshot Snap(int oppHp, int oppArmor = 0, int secrets = 0)
        {
            var s = new GameSnapshot { MyTurn = true };
            s.Me.HeroHealth = 30;
            s.Opp.HeroHealth = oppHp;
            s.Opp.Armor = oppArmor;
            s.Opp.Secrets = secrets;
            return s;
        }

        [Fact]
        public void ObviousLethal_IsProven()
        {
            var s = Snap(8);
            s.Me.Board.Add(Mine(5, 5));
            s.Me.Board.Add(Mine(3, 3));
            var r = LethalSolver.Solve(s);
            Assert.Equal(LethalStatus.Proven, r.Status);
            Assert.Equal(8, r.FaceDamage);
            Assert.All(r.Actions, a => Assert.Equal(LethalSolver.Face, a.TargetId));
        }

        [Fact]
        public void OneShortOfLethal_IsNone()
        {
            var s = Snap(9);
            s.Me.Board.Add(Mine(5, 5));
            s.Me.Board.Add(Mine(3, 3));
            Assert.Equal(LethalStatus.None, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void Armor_CountsAsHealth()
        {
            var s = Snap(5, oppArmor: 4);
            s.Me.Board.Add(Mine(5, 5));
            s.Me.Board.Add(Mine(3, 3));
            Assert.Equal(LethalStatus.None, LethalSolver.Solve(s).Status);
            s.Opp.Armor = 3;
            Assert.Equal(LethalStatus.Proven, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void Taunt_FalseLethal()
        {
            var s = Snap(8);
            s.Me.Board.Add(Mine(5, 5));
            s.Me.Board.Add(Mine(3, 3));
            s.Opp.Board.Add(Taunt(2, 4));
            // Must spend an attacker on Taunt; remaining damage < 8.
            Assert.Equal(LethalStatus.None, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void Taunt_LethalRequiresTradeFirst()
        {
            var s = Snap(7);
            s.Me.Board.Add(Mine(4, 4));
            s.Me.Board.Add(Mine(7, 2));
            s.Opp.Board.Add(Taunt(1, 4));
            var r = LethalSolver.Solve(s);
            Assert.Equal(LethalStatus.Proven, r.Status);
            Assert.NotEqual(LethalSolver.Face, r.Actions.First().TargetId); // first action hits the Taunt
            Assert.Equal(7, r.FaceDamage);
        }

        [Fact]
        public void DivineShieldTaunt_NeedsTwoHits()
        {
            var s = Snap(5);
            s.Me.Board.Add(Mine(1, 5));
            s.Me.Board.Add(Mine(1, 5));
            s.Me.Board.Add(Mine(5, 5));
            s.Opp.Board.Add(Taunt(0, 1, shield: true));
            var r = LethalSolver.Solve(s);
            Assert.Equal(LethalStatus.Proven, r.Status); // two 1-power swings pop shield + kill, 5-power goes face
            Assert.Equal(5, r.FaceDamage);
        }

        [Fact]
        public void Windfury_ProvidesTwoSwings()
        {
            var s = Snap(8);
            s.Me.Board.Add(Mine(4, 4, attacks: 2));
            var r = LethalSolver.Solve(s);
            Assert.Equal(LethalStatus.Proven, r.Status);
            Assert.Equal(2, r.Actions.Count);
        }

        [Fact]
        public void Poisonous_KillsBigTaunt()
        {
            var s = Snap(3);
            s.Me.Board.Add(Mine(1, 1, poison: true));
            s.Me.Board.Add(Mine(3, 3));
            s.Opp.Board.Add(Taunt(2, 20));
            Assert.Equal(LethalStatus.Proven, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void StealthedTaunt_DoesNotBlock()
        {
            var s = Snap(5);
            s.Me.Board.Add(Mine(5, 5));
            s.Opp.Board.Add(Taunt(3, 3, stealth: true));
            Assert.Equal(LethalStatus.Proven, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void ImmuneTaunt_CannotBeCleared()
        {
            var s = Snap(5);
            s.Me.Board.Add(Mine(9, 9));
            s.Opp.Board.Add(Taunt(1, 1, immune: true));
            Assert.Equal(LethalStatus.None, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void RushMinion_CanClearTauntButNotHitFace()
        {
            var s = Snap(3);
            s.Me.Board.Add(Mine(4, 4, canHero: false)); // fresh Rush
            s.Me.Board.Add(Mine(3, 3));
            s.Opp.Board.Add(Taunt(1, 4));
            var r = LethalSolver.Solve(s);
            Assert.Equal(LethalStatus.Proven, r.Status);
            Assert.Equal(3, r.FaceDamage);
        }

        [Fact]
        public void HeroWeaponAttack_CountsAndSurvivalIsChecked()
        {
            var s = Snap(3);
            s.Me.HeroAttack = 3;
            s.Me.HeroAttacksRemaining = 1;
            Assert.Equal(LethalStatus.Proven, LethalSolver.Solve(s).Status);

            // Hero must not die attacking into a Taunt.
            s = Snap(3);
            s.Me.HeroHealth = 2; s.Me.HeroAttack = 3; s.Me.HeroAttacksRemaining = 1;
            s.Me.Board.Add(Mine(1, 1));
            s.Opp.Board.Add(Taunt(5, 3));
            Assert.Equal(LethalStatus.None, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void DivineShield_AttackerSurvivesTauntAndKeepsSecondSwing()
        {
            var s = Snap(4);
            s.Me.Board.Add(Mine(2, 1, attacks: 2, shield: true)); // windfury + shield
            s.Opp.Board.Add(Taunt(5, 2));
            var r = LethalSolver.Solve(s);
            // Swing 1 kills Taunt (shield absorbs counter), swing 2 face = 2 damage, not 4.
            Assert.Equal(LethalStatus.None, r.Status);
        }

        [Fact]
        public void EnemySecret_CapsAtLikely()
        {
            var s = Snap(5, secrets: 1);
            s.Me.Board.Add(Mine(5, 5));
            var r = LethalSolver.Solve(s);
            Assert.Equal(LethalStatus.Likely, r.Status);
            Assert.Contains(r.Reasons, x => x.Contains("secret"));
        }

        [Fact]
        public void UnknownEnemyCard_CapsAtLikely()
        {
            var s = Snap(5);
            s.Me.Board.Add(Mine(5, 5));
            s.Opp.Board.Add(new MinionState { Id = 1, CardId = "X", Attack = 1, Health = 1, Support = Support.Unknown });
            Assert.Equal(LethalStatus.Likely, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void NotMyTurn_NoLethalClaim()
        {
            var s = Snap(1);
            s.MyTurn = false;
            s.Me.Board.Add(Mine(5, 5));
            Assert.Equal(LethalStatus.None, LethalSolver.Solve(s).Status);
        }

        [Fact]
        public void BudgetExhausted_IsInconclusiveNeverNone()
        {
            var s = Snap(14);
            for (int i = 0; i < 7; i++) s.Me.Board.Add(Mine(2, 5));
            for (int i = 0; i < 4; i++) s.Opp.Board.Add(Taunt(1, 6));
            var r = LethalSolver.Solve(s, nodeBudget: 5);
            Assert.Equal(LethalStatus.Inconclusive, r.Status);
        }

        [Fact]
        public void Cancellation_IsInconclusive()
        {
            var s = Snap(100);
            s.Me.Board.Add(Mine(2, 5));
            s.Opp.Board.Add(Taunt(1, 6));
            var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.Equal(LethalStatus.Inconclusive, LethalSolver.Solve(s, token: cts.Token).Status);
        }

        [Fact]
        public void Signature_ChangesWhenBoardChanges()
        {
            var s = Snap(10);
            s.Me.Board.Add(Mine(2, 2));
            var a = s.Signature();
            s.Me.Board[0].Attack = 3;
            Assert.NotEqual(a, s.Signature());
        }
    }
}
