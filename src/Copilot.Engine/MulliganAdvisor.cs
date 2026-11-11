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
        public double? WinRate { get; set; }
    }

    /// <summary>
    /// Keep/replace advice for the Zoo Warlock list in docs/deck-cards.md, driven by the per-card "Mulligan WR"
    /// the user supplied (see docs/mulligan-data.md). It is the win rate of games in which the card was kept, so it
    /// carries selection bias (cards that are rarely kept are only kept in good hands). Cards without data fall back
    /// to "cheap minion = keep". Pure function, unit-tested.
    /// </summary>
    public static class MulliganAdvisor
    {
        /// <summary>Keep when the card's Mulligan WR is at least this many percent.</summary>
        public const double KeepThreshold = 64.0;

        // CardId -> Mulligan WR (%). Source: user's deck statistics screenshot, 2026-11-11.
        private static readonly Dictionary<string, double> Wr = new Dictionary<string, double>
        {
            { "BE_085", 71.1 },       // Ritual of Summoning
            { "BE_726", 69.8 },       // Ritual of C'Thun
            { "BE_094", 68.5 },       // Floating Lessons
            { "CATA_210", 65.6 },     // Twilight Egg
            { "CAP_405", 65.6 },      // Godfather Kazakus
            { "TIME_025", 65.4 },     // Twilight Timehopper
            { "CORE_UNG_205", 63.4 }, // Glacial Shard
            { "BE_086", 62.7 },       // Blasphemy
            { "TIME_027", 62.2 },     // Tachyon Barrage
            { "TLC_451", 62.0 },      // Cursed Catacombs
            { "CORE_SCH_713", 61.8 }, // Cult Neophyte
            { "TIME_026", 61.6 },     // Entropic Continuity
            { "BE_728", 61.6 },       // Zakajz the Corruptor
            { "BE_729", 59.7 },       // Mawman Cultist
            { "TIME_029", 59.1 },     // Ruinous Velocidrake
            { "BE_151", 54.5 },       // Vaultkeeper
        };

        public static List<MulliganPick> Advise(GameSnapshot s)
        {
            var picks = new List<MulliganPick>();
            foreach (var c in s.Me.Hand.Where(h => h.CardId != "GAME_005").OrderBy(h => h.Cost)) // The Coin is not a choice
            {
                var pick = new MulliganPick { Name = c.Name ?? c.CardId, Cost = c.Cost };
                if (c.CardId != null && Wr.TryGetValue(c.CardId, out var w))
                {
                    pick.WinRate = w;
                    pick.Keep = w >= KeepThreshold;
                    pick.Reason = w.ToString("0.0") + "% WR";
                }
                else
                {
                    pick.Keep = c.Kind == CardKind.Minion && c.Cost <= 3;
                    pick.Reason = "no data, cost rule";
                }
                picks.Add(pick);
            }
            return picks;
        }

        public static string Format(GameSnapshot s, List<MulliganPick> picks)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(s.OnThePlay ? "MULLIGAN (on the play)" : "MULLIGAN (on the draw)");
            foreach (var p in picks.OrderBy(x => x.Keep ? 0 : 1).ThenByDescending(x => x.WinRate ?? 0))
                sb.AppendLine((p.Keep ? "KEEP    " : "REPLACE ") + p.Cost + "  " + p.Name + "  - " + p.Reason);
            sb.Append("Keep = Mulligan WR >= " + KeepThreshold.ToString("0") + "% (your deck stats)");
            return sb.ToString();
        }
    }
}
