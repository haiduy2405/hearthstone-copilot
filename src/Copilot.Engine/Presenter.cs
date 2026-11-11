using System.Collections.Generic;
using System.Linq;
using System.Text;
using Copilot.Engine.Lethal;
using Copilot.Engine.Model;

namespace Copilot.Engine
{
    /// <summary>Turns a snapshot + result into the compact panel text. Pure, so it is unit-testable.</summary>
    public static class Presenter
    {
        public static string Format(GameSnapshot s, LethalResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("COPILOT");
            if (!s.MyTurn) { sb.Append("Opponent's turn"); return sb.ToString(); }

            switch (r.Status)
            {
                case LethalStatus.Proven: sb.AppendLine("LETHAL: YES (verified)"); break;
                case LethalStatus.Likely: sb.AppendLine("LETHAL: LIKELY"); break;
                case LethalStatus.None: sb.Append("LETHAL: NO (attacks only)"); return sb.ToString();
                default: sb.AppendLine("LETHAL: UNKNOWN"); break;
            }

            if (r.Status == LethalStatus.Proven || r.Status == LethalStatus.Likely)
            {
                var names = Names(s);
                int n = 1;
                foreach (var step in CompactSteps(r.Actions))
                    sb.AppendLine(n++ + ". " + Name(names, step.AttackerId) + " -> " + (step.TargetId == LethalSolver.Face ? "FACE" : Name(names, step.TargetId)));
                sb.AppendLine("Damage " + r.FaceDamage + " / " + r.RequiredDamage);
            }
            foreach (var reason in r.Reasons.Distinct())
                sb.AppendLine("! " + reason);
            return sb.ToString().TrimEnd();
        }

        private static IEnumerable<PlannedAttack> CompactSteps(List<PlannedAttack> actions) => actions;

        private static Dictionary<int, string> Names(GameSnapshot s)
        {
            var d = new Dictionary<int, string> { { LethalSolver.HeroAttacker, "Hero" } };
            foreach (var m in s.Me.Board.Concat(s.Opp.Board)) d[m.Id] = m.Name ?? m.CardId ?? ("#" + m.Id);
            return d;
        }

        private static string Name(Dictionary<int, string> names, int id) =>
            names.TryGetValue(id, out var n) ? n : "#" + id;
    }
}
