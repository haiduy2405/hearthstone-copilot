using System.Collections.Generic;
using System.Linq;
using Copilot.Engine.Model;

namespace Copilot.Engine
{
    public sealed class MulliganPick
    {
        public string Name { get; set; }
        public int Cost { get; set; }
        public bool Keep { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// Keep/replace advice for the starting hand of the Zoo Warlock deck in docs/deck-cards.md.
    /// The per-card values are hand-written judgement from card text (cost, stats, whether the card needs a board),
    /// NOT win-rate data. Unknown cards fall back to a cost-based rule. Pure function, unit-tested.
    /// </summary>
    public static class MulliganAdvisor
    {
        // Value 0-10: how much a Zoo hand wants this card in the opening. Keep threshold is 6.
        private static readonly Dictionary<string, int> Value = new Dictionary<string, int>
        {
            { "TIME_025", 9 }, // Twilight Timehopper: 4/4 for 2 (cost: self damage later)
            { "BE_729", 8 },   // Mawman Cultist: 4/4 for 3 (taxes our own spells)
            { "CORE_SCH_713", 7 }, // Cult Neophyte: 3/2 for 2
            { "CORE_UNG_205", 6 }, // Glacial Shard: 2/1 for 1 + Freeze
            { "CAP_405", 6 },  // Godfather Kazakus: 3/3 for 3, custom battlecry
            { "BE_728", 6 },   // Zakajz the Corruptor: 5/5 for 4
            { "CATA_210", 5 }, // Twilight Egg: 0/2 for 1, grows later
            { "TIME_029", 5 }, // Ruinous Velocidrake: 5/5 Rush for 5
            { "TLC_451", 4 },  // Cursed Catacombs: free Discover
            { "BE_085", 4 },   // Ritual of Summoning: delayed 2x 2/4 Taunt
            { "BE_094", 4 },   // Floating Lessons: needs a board
            { "BE_151", 4 },   // Vaultkeeper: 3/3 for 5, needs a board
            { "BE_086", 3 },   // Blasphemy: reactive AoE
            { "TIME_026", 3 }, // Entropic Continuity: needs a board
            { "TIME_027", 3 }, // Tachyon Barrage: finisher
            { "BE_726", 2 },   // Ritual of C'Thun: slow
        };

        public static List<MulliganPick> Advise(GameSnapshot s)
        {
            var picks = new List<MulliganPick>();
            var hand = s.Me.Hand;
            var seenExpensive = new HashSet<string>();
            int expensiveKept = 0;
            int cheapKept = 0;

            foreach (var c in hand.OrderBy(h => h.Cost))
            {
                var pick = new MulliganPick { Name = c.Name ?? c.CardId, Cost = c.Cost };
                int v;
                bool known = c.CardId != null && Value.TryGetValue(c.CardId, out v);
                v = known ? Value[c.CardId] : (c.Kind == CardKind.Minion && c.Cost <= 3 ? 6 : 3);
                bool expensive = c.Cost >= 4;

                if (v < 6) { pick.Keep = false; pick.Reason = known ? "weak in an opening hand" : "unknown card, not cheap"; }
                else if (expensive)
                {
                    // Only a second expensive card is a problem; on the draw one expensive card is fine with cheap plays.
                    if (expensiveKept >= 1 || cheapKept < 2) { pick.Keep = false; pick.Reason = "too slow with this hand"; }
                    else { pick.Keep = true; pick.Reason = "strong, hand has cheap plays"; expensiveKept++; }
                }
                else { pick.Keep = true; pick.Reason = c.Cost <= 2 ? "cheap play" : "good on curve"; cheapKept++; }

                if (!known && pick.Keep) pick.Reason += " (not in table, cost rule)";
                if (c.Cost >= 4 && !pick.Keep && !seenExpensive.Add(c.CardId ?? "")) pick.Reason = "duplicate expensive card";
                picks.Add(pick);
            }

            // A hand with nothing to do on turn 1-2 must keep its best cheap card.
            if (!picks.Any(p => p.Keep && p.Cost <= 2))
            {
                var best = picks.Where(p => p.Cost <= 2).OrderByDescending(p => ValueOf(hand, p)).FirstOrDefault();
                if (best != null && ValueOf(hand, best) >= 4) { best.Keep = true; best.Reason = "keep one early play"; }
            }
            return picks;
        }

        private static int ValueOf(List<HandCard> hand, MulliganPick p)
        {
            var c = hand.First(h => (h.Name ?? h.CardId) == p.Name);
            return c.CardId != null && Value.TryGetValue(c.CardId, out var v) ? v : 3;
        }

        public static string Format(GameSnapshot s, List<MulliganPick> picks)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(s.OnThePlay ? "MULLIGAN (on the play)" : "MULLIGAN (on the draw)");
            foreach (var p in picks.OrderBy(x => x.Keep ? 0 : 1).ThenBy(x => x.Cost))
                sb.AppendLine((p.Keep ? "KEEP    " : "REPLACE ") + p.Cost + "  " + p.Name + "  - " + p.Reason);
            sb.Append("Hand-written ranking, not win-rate data");
            return sb.ToString();
        }
    }
}
