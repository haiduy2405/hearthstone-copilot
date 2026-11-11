using Copilot.Engine;
using Copilot.Engine.Lethal;
using Copilot.Engine.Model;
using Xunit;

namespace Copilot.Tests
{
    public class PanelViewTests
    {
        private static GameSnapshot Snap(int oppHp)
        {
            var s = new GameSnapshot { MyTurn = true };
            s.Me.HeroHealth = 30;
            s.Opp.HeroHealth = oppHp;
            return s;
        }

        private static MinionState Mine(int atk) =>
            new MinionState { Id = 1, Name = "Bruiser", Attack = atk, Health = 5, AttacksRemaining = 1, Support = Support.Supported };

        [Fact]
        public void NoLethal_NoDanger_IsHidden()
        {
            var s = Snap(30);
            s.Me.Board.Add(Mine(3));
            var v = Presenter.Build(s, LethalSolver.Solve(s));
            Assert.Equal(PanelLevel.Hidden, v.Level);
        }

        [Fact]
        public void ProvenLethal_ShowsBigHeadline_StepsOnlyInDetails()
        {
            var s = Snap(3);
            s.Me.Board.Add(Mine(3));
            var v = Presenter.Build(s, LethalSolver.Solve(s));
            Assert.Equal(PanelLevel.Lethal, v.Level);
            Assert.Equal("LETHAL", v.Headline);
            Assert.DoesNotContain("->", v.Headline);
            Assert.Contains("Bruiser -> FACE", v.Details);
        }

        [Fact]
        public void LikelyLethal_ShowsQuestionMark()
        {
            var s = Snap(3);
            s.Opp.Secrets = 1;
            s.Me.Board.Add(Mine(3));
            var v = Presenter.Build(s, LethalSolver.Solve(s));
            Assert.Equal(PanelLevel.Likely, v.Level);
            Assert.Equal("LETHAL?", v.Headline);
            Assert.Contains("secret", v.Details);
        }

        [Fact]
        public void OpponentsTurn_IsHidden()
        {
            var s = Snap(3);
            s.MyTurn = false;
            Assert.Equal(PanelLevel.Hidden, Presenter.Build(s, LethalSolver.Solve(s)).Level);
        }

        [Fact]
        public void VisibleDanger_WithoutLethal_ShowsDanger()
        {
            var s = Snap(30);
            s.Me.HeroHealth = 6;
            s.Opp.Board.Add(new MinionState { Id = 9, Name = "Big", Attack = 7, Health = 7, MaxAttacks = 1 });
            var v = Presenter.Build(s, LethalSolver.Solve(s));
            Assert.Equal(PanelLevel.Danger, v.Level);
            Assert.Equal("DANGER", v.Headline);
        }

        [Fact]
        public void Lethal_TakesPrecedenceOverDanger()
        {
            var s = Snap(3);
            s.Me.HeroHealth = 6;
            s.Me.Board.Add(Mine(3));
            s.Opp.Board.Add(new MinionState { Id = 9, Name = "Big", Attack = 7, Health = 7, MaxAttacks = 1 });
            
            var v = Presenter.Build(s, LethalSolver.Solve(s));
            Assert.True(v.Level == PanelLevel.Lethal || v.Level == PanelLevel.Likely);
        }
    }
}
