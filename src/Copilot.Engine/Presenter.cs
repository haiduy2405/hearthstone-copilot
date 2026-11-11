using System.Collections.Generic;
using System.Linq;
using System.Text;
using Copilot.Engine.Lethal;
using Copilot.Engine.Model;

namespace Copilot.Engine
{
    public enum PanelLevel { Hidden = 0, Danger = 1, Likely = 2, Lethal = 3, Mulligan = 4 }

    /// <summary>What the panel should show right now. Hidden means: draw nothing.</summary>
    public sealed class PanelView
    {
        public PanelLevel Level { get; set; }
        public string Headline { get; set; }
        /// <summary>Full explanation (steps, damage, caveats) shown only when the player expands the panel.</summary>
        public string Details { get; set; }
        /// <summary>Show Details without the player asking (mulligan: the details are the whole point).</summary>
        public bool AutoExpand { get; set; }
    }

    /// <summary>Turns a snapshot + result into the compact panel text. Pure, so it is unit-testable.</summary>
    public static class Presenter
    {
        /// <summary>
        /// Minimal-noise view: a big headline only when there is lethal (or the player is in visible danger);
        /// everything else is hidden. The step list lives in Details and is shown on demand.
        /// </summary>
        public static PanelView Build(GameSnapshot s, LethalResult r)
        {
            var hidden = new PanelView { Level = PanelLevel.Hidden };
            if (s.InMulligan)
            {
                if (s.Me.Hand.Count == 0) return hidden;
                var picks = MulliganAdvisor.Advise(s);
                int keep = picks.Count(p => p.Keep);
                return new PanelView
                {
                    Level = PanelLevel.Mulligan,
                    Headline = "KEEP " + keep + " / SWAP " + (picks.Count - keep),
                    Details = MulliganAdvisor.Format(s, picks),
                    AutoExpand = true
                };
            }
            if (!s.MyTurn) return hidden;

            if (r.Status == LethalStatus.Proven)
                return new PanelView { Level = PanelLevel.Lethal, Headline = "LETHAL", Details = Format(s, r) };
            if (r.Status == LethalStatus.Likely)
                return new PanelView { Level = PanelLevel.Likely, Headline = "LETHAL?", Details = Format(s, r) };

            var t = ThreatAnalyzer.Analyze(s);
            if (t.Danger)
            {
                var sb = new StringBuilder();
                AppendThreat(sb, s);
                sb.AppendLine("Not counted: enemy hand (charge/burn), hero power, weapon swaps.");
                if (s.Opp.Secrets > 0) sb.AppendLine(s.Opp.Secrets + " enemy secret(s) in play.");
                return new PanelView { Level = PanelLevel.Danger, Headline = "DANGER", Details = sb.ToString().TrimEnd() };
            }
            return hidden;
        }

        public static string Format(GameSnapshot s, LethalResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("COPILOT");
            if (!s.MyTurn) { sb.Append("Opponent's turn"); return sb.ToString(); }

            switch (r.Status)
            {
                case LethalStatus.Proven: sb.AppendLine("LETHAL: YES (verified)"); break;
                case LethalStatus.Likely: sb.AppendLine("LETHAL: LIKELY"); break;
                case LethalStatus.None: sb.AppendLine("LETHAL: NO (modelled actions)"); AppendThreat(sb, s); return sb.ToString().TrimEnd();
                default: sb.AppendLine("LETHAL: UNKNOWN"); break;
            }

            if (r.Status == LethalStatus.Proven || r.Status == LethalStatus.Likely)
            {
                var names = Names(s);
                int n = 1;
                foreach (var step in r.Actions)
                    sb.AppendLine(n++ + ". " + Describe(step, names));
                sb.AppendLine("Damage " + r.FaceDamage + " / " + r.RequiredDamage);
            }
            foreach (var reason in r.Reasons.Distinct())
                sb.AppendLine("! " + reason);
            return sb.ToString().TrimEnd();
        }

        private static void AppendThreat(StringBuilder sb, GameSnapshot s)
        {
            var t = ThreatAnalyzer.Analyze(s);
            if (t.Danger)
                sb.AppendLine("DANGER: enemy board shows " + t.VisibleDamage + " dmg, you have " + t.HeroEffectiveHealth + (t.HasOurTaunt ? " (your Taunt may block)" : ""));
            else if (t.Watch)
                sb.AppendLine("Watch: enemy board shows " + t.VisibleDamage + " dmg, you have " + t.HeroEffectiveHealth);
        }

        public static string Describe(PlannedAction a, Dictionary<int, string> names)
        {
            if (a.Kind == ActionKind.Play)
            {
                var text = "Play " + Name(names, a.CardEntityId);
                if (a.Slot >= 0) text += " (slot " + (a.Slot + 1) + " from left)";
                if (a.TargetId != LethalSolver.NoTarget) text += " on " + Name(names, a.TargetId);
                return text;
            }
            return Name(names, a.AttackerId) + " -> " + (a.TargetId == LethalSolver.Face ? "FACE" : Name(names, a.TargetId));
        }

        private static Dictionary<int, string> Names(GameSnapshot s)
        {
            var d = new Dictionary<int, string> { { LethalSolver.HeroAttacker, "Hero" } };
            foreach (var m in s.Me.Board.Concat(s.Opp.Board)) d[m.Id] = m.Name ?? m.CardId ?? ("#" + m.Id);
            foreach (var h in s.Me.Hand) d[h.Id] = h.Name ?? h.CardId ?? ("#" + h.Id);
            return d;
        }

        private static string Name(Dictionary<int, string> names, int id) =>
            names.TryGetValue(id, out var n) ? n : "#" + id;
    }
}
